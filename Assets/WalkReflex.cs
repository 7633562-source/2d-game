using System.Globalization;
using UnityEngine;

// WALK REFLEX (27.09, task "Demo walk: step-up mechanism + tune", game line).
//
// WHY. The pose walk holds ONE ground datum, taken at the stand frame, and asks
// the swing sole for heights measured against it. On the shipped yard tape that
// plane is wrong 8 m in: the CurbEntry slab is +5 cm (x 8..11), the tape dips
// -5 cm at the DrainDip (x 18) and rises again at the CurbReturn (x 22). The
// accepted line stubs the first lip at 20.10 s of the demo window and topples
// BACKWARD - measured, reproduced, and not reachable by the 24 tuning dims of
// the evolution run (report evo-demo-walk-20260927.md, sections 3 and 4). The
// missing thing is a capability, not a number: nothing ever looks at the floor
// ahead, and nothing answers a body carried backwards.
//
// WHAT. Two reflexes, both cheap and both read from the world:
//   1) FLOOR RISE -> the walk's local datum. A raycast looks `lookAheadM` ahead
//      of the pelvis (GroundLayers - the same layer the feet stand on); the
//      floor found there becomes the datum for the incoming step, rate-limited
//      to riseRateMps and bounded by riseMaxM, plus a bounded extra sole
//      clearance while the rise is still ahead (liftBoostM). The pose library's
//      descent and clearance arithmetic is hip-relative (PoseDriveController:
//      FloorDatumM), so raising the datum raises the whole step: the swing foot
//      clears the lip and lands ON the new floor; when the floor drops the
//      datum follows it down and the step becomes a step down.
//   2) BACKWARD STUMBLE -> one recovery step. The thresholds are handed to the
//      pose walk, where the forced swap lives (PoseDriveController.TickWalk,
//      the swap block) - the placement law turns a backward-moving CoM into a
//      backward foot by itself, so the reflex only has to release the step.
//
// The component is the ONLY writer of the pose controller's Reflex* fields, so
// a trial without it (or with it inert) reproduces the landed rows bit for bit.
// Attachment: the played/WebGL human carries it from GameProcess; the headless
// yard replica arms it with `-walkReflex 1`. Its own switches:
//   -walkStepUp 0/1   -walkStepLook m   -walkStepRate m/s   -walkStepMax m
//   -walkStepLift m   -walkStumble 0/1  -walkStumbleV m/s   -walkStumbleHold s
[DefaultExecutionOrder(-60)]
public class WalkReflex : MonoBehaviour
{
    // Defaults are the shipped demo's own (the WebGL page has no command line);
    // the trial passes them explicitly and can switch either half off.
    // 28.09 (task "Demo ship: walkable build"): the defaults below are the SHIP
    // combo proven by the v2 ledger - look 0.60, lift 0.06, flick hold armed,
    // step-up gain 3.0 with the walk's own lead gate 0.05 (the curb climb:
    // 0.7 m walked up, alive 20.285 s / travel 12.2581 m on the demo condition,
    // flat guard 46.92 s with the same tree). Changing a default here moves the
    // shipped page and every future run that does not pass that flag.
    public bool stepUp = true;
    public bool stumble = true;
    [Tooltip("Metres ahead of the pelvis where the floor is probed.")]
    public float lookAheadM = 0.60f;
    [Tooltip("Fastest the local floor datum may travel, m/s.")]
    public float riseRateMps = 0.35f;
    [Tooltip("Largest floor step the reflex will follow, m.")]
    public float riseMaxM = 0.09f;
    [Tooltip("Extra swing sole clearance while a rise is still ahead, m.")]
    public float liftBoostM = 0.060f;
    [Tooltip("Rise at which the extra clearance is fully applied, m.")]
    public float riseFullM = 0.030f;
    // WALK REFLEX (27.09, curb step-up iteration): the probe's own seat and the
    // datum ramp's own discipline, both OFF by default (the shipped demo line).
    //   aimPlacement - probe the toe of the PLANNED landing (`ReflexPlaceX` +
    //   aimToeM) instead of a fixed look-ahead off the pelvis: the rise is then
    //   read at the point the incoming sole is actually aimed at.
    //   dualRamp     - follow the rise only while both soles are down; in single
    //   support the ramp runs at singleRateMps (0 = it waits). The pelvis is
    //   never asked to stand at the new plane while the only support under it is
    //   still on the old one.
    [Tooltip("Probe the toe of the planned landing (ReflexPlaceX + aimToeM).")]
    public bool aimPlacement;
    [Tooltip("Sole length ahead of the plan point (heel share), m.")]
    public float aimToeM = 0.13f;
    [Tooltip("Follow the rise only while both soles are down.")]
    public bool dualRamp;
    [Tooltip("Rise rate in single support, m/s (with dualRamp; 0 = wait).")]
    public float singleRateMps;
    // WALK REFLEX (27.09, the push-off's own hold): the walk's final push is its
    // roll onto the TOE (pushDeliverP, TickWalk). At a riser that roll presses the
    // toe into the wall instead of the floor: the step that is standing at the
    // riser is delivered without the amplified flick (PoseDriveController's
    // ReflexHoldPush), which also cannot shove a body still on the lower plane.
    //   flickGate - arm the hold; flickZoneM - how far from the edge the toe may
    //   stand and still hold it; flickHoldS - how long the hold survives after
    //   the toe leaves the edge.
    [Tooltip("Hold the push flick while a sole stands at a riser.")]
    public bool flickGate = true;
    [Tooltip("How far from the riser edge the hold still applies, m.")]
    public float flickZoneM = 0.10f;
    [Tooltip("Seconds the hold survives after the toe leaves the riser zone, s.")]
    public float flickHoldS = 0.25f;
    // WALK REFLEX v2 (28.09, the rise's own step-up): while a sole stands ON the
    // top surface of a rise the leg's support delivery is scaled by stepUpGain
    // (one factor per leg, PoseDriveController.ReflexStepUpL/R) - the climb's own
    // work, delivered. The sole must be UP there first: a low-plane sole gets
    // nothing. 0 = off, the landed expression bit for bit.
    [Tooltip("Rise-gated step-up delivery factor (0 = off).")]
    public float stepUpGain = 3.0f;
    [Tooltip("How far below the top surface a sole still counts as ON it, m.")]
    public float stepUpPadM = 0.02f;
    [Tooltip("Seconds the delivery survives after the sole leaves the top, s.")]
    public float stepUpHoldS = 0.10f;
    [Tooltip("How far past the riser the body may be before the delivery lapses, m.")]
    public float stepUpPastM = 0.30f;
    [Tooltip("How far the pelvis must be PAST the leg's foot before its delivery is live, m (the walk's own lead rule: a push delivered while the CoM is still behind the foot topples the body).")]
    public float stepUpLeadM = 0.05f;
    [Tooltip("Pelvis speed (m/s) below which the delivery is live at all (0 = no speed gate): the assist fires only when the walk is actually running out of steam at a rise, and keeps firing past the edge until it has its pace back.")]
    public float stepUpSpeedMps;
    [Tooltip("The hip's share of the step-up factor (0 = the hip keeps its landed pair).")]
    public float stepUpHipShare;
    // WALK REFLEX v2b (28.09, the pace-recovery seat's first lever): how much of the
    // step-up boost the ANKLE's own stiffness keeps. 1 = the ship (the boosted
    // kneeP rides into the ankle, so the rise's delivery is the knee's drive AND
    // the plantar roll - the vault that outran its own support in 8.7); 0 = the
    // knee only (a compliant toe: the climb is delivered without the launch).
    [Tooltip("Share of the step-up boost the ankle's stiffness keeps (1 = knee+plantar, 0 = knee only).")]
    public float stepUpAnkleShare = 1f;
    // WALK REFLEX v2c (28.09, the pace-recovery seat's cadence law - the 8.7 read's
    // own missing piece): the walk's step clock runs at 1/cadenceScale while the
    // walk's own pace is ABOVE cadenceVelMps. The mechanism's climb leaves the body
    // at 1.0-1.3 m/s at the top of the curb - above anything the landed cadence can
    // catch - and the landed walk holds 0.5-0.75 on the flat, so the law is inert
    // on the flat by construction and fires only when the walk outruns its own
    // cadence. 1 (or cadenceVelMps 0) = the landed cadence bit for bit.
    [Tooltip("Step-clock scale while the pace is above cadenceVelMps (1 = the landed cadence).")]
    public float cadenceScale = 1f;
    [Tooltip("Pace (m/s) above which the faster cadence applies (0 = off).")]
    public float cadenceVelMps;
    // WALK REFLEX v2d (28.09): the burst. With a sustained faster clock the walk
    // self-accelerates (the placement law follows the pace): the catch turned into
    // a 1.5 m/s scramble and pitched over. With cadenceBurstS > 0 the faster clock
    // is a BURST - it fires when the pace first crosses the trigger, runs for
    // cadenceBurstS, then returns to the landed cadence and re-arms only after the
    // pace has come back below the trigger (hysteresis). 0 = the sustained version.
    [Tooltip("Seconds of faster cadence per pace crossing (0 = sustained while fast).")]
    public float cadenceBurstS;
    private float cadenceBurstT;
    private bool cadenceArmed = true;
    // WALK REFLEX v2e (28.09): the placement's own pace seats - the step band's
    // ceiling scale and the catch share of the pendulum constant. The landed band
    // (~0.59 m span at the 0.85 s cycle) is the walk's own envelope: above ~0.7 m/s
    // the plan cannot reach past the body and the walk pitches over; the landed
    // catch (half the constant) asks a lead the walk can take back but bleeds no
    // surplus. Both fire only while the pace is above their own trigger; 1 / 0.5 (or
    // a 0 trigger) = the landed law bit for bit.
    [Tooltip("Step-band ceiling scale while the pace is above bandVelMps (1 = landed).")]
    public float bandScale = 1f;
    [Tooltip("Pace (m/s) above which the wider step band applies (0 = off).")]
    public float bandVelMps;
    [Tooltip("Catch share of the pendulum constant while the pace is above brakeVelMps (0.5 = landed).")]
    public float catchShare = 0.5f;
    [Tooltip("Pace (m/s) above which the catch share applies (0 = off).")]
    public float brakeVelMps;
    // WALK REFLEX v2f (28.09): the uprightness seat - a trunk-back lean added to the
    // walk's own torso target while the pace outruns the cadence the walk was tuned
    // at (the same trigger the burst uses). The trunk carries ~40% of the mass, so
    // the lean is a CoM lever: it holds the body's mass back over the plan while the
    // surplus speed of a climb bleeds off. 0 (or a 0 trigger) = the landed pose bit
    // for bit.
    [Tooltip("Trunk-back lean, deg, added to the walk's torso target above leanVelMps (0 = off).")]
    public float torsoLeanDeg;
    [Tooltip("Pace (m/s) above which the trunk lean applies (0 = off).")]
    public float leanVelMps;
    private float stepUpHoldL, stepUpHoldR;
    private float lastEyeX;
    private float eyeSpeedEma;
    private float riserEdgeX = float.NaN;   // where the floor ahead steps up (probed)
    private float flickHoldT;
    [Tooltip("Backward CoM speed that releases one recovery step, m/s.")]
    public float stumbleVelMps = 0.30f;
    [Tooltip("Seconds the backward drift must hold before the step, s.")]
    public float stumbleHoldS = 0.10f;

    private PoseDriveController pose;
    private float floorOffsetM;
    private float floorRefY = float.NaN;   // the floor the walk STARTED on (probed)
    private float lastFloorY = float.NaN;
    private float logClock;
    public bool logTrace;               // -walkLog 1: one diagnostic line every 0.25 s

    void Awake()
    {
        stepUp = GetFloat("-walkStepUp", stepUp ? 1f : 0f) > 0.5f;
        stumble = GetFloat("-walkStumble", stumble ? 1f : 0f) > 0.5f;
        lookAheadM = GetFloat("-walkStepLook", lookAheadM);
        riseRateMps = Mathf.Max(0.01f, GetFloat("-walkStepRate", riseRateMps));
        riseMaxM = Mathf.Clamp(GetFloat("-walkStepMax", riseMaxM), 0.01f, 0.25f);
        liftBoostM = Mathf.Clamp(GetFloat("-walkStepLift", liftBoostM), 0f, 0.08f);
        riseFullM = Mathf.Max(0.002f, GetFloat("-walkStepFull", riseFullM));
        aimPlacement = GetFloat("-walkStepAim", aimPlacement ? 1f : 0f) > 0.5f;
        aimToeM = Mathf.Max(0f, GetFloat("-walkStepAimToe", aimToeM));
        dualRamp = GetFloat("-walkStepDual", dualRamp ? 1f : 0f) > 0.5f;
        singleRateMps = Mathf.Max(0f, GetFloat("-walkStepSingleRate", singleRateMps));
        flickGate = GetFloat("-walkStepFlick", flickGate ? 1f : 0f) > 0.5f;
        flickZoneM = Mathf.Max(0.02f, GetFloat("-walkStepFlickZone", flickZoneM));
        flickHoldS = Mathf.Clamp(GetFloat("-walkStepFlickHold", flickHoldS), 0f, 1.0f);
        stepUpGain = Mathf.Max(0f, GetFloat("-walkStepUp", stepUpGain));
        stepUpPadM = Mathf.Clamp(GetFloat("-walkStepUpPad", stepUpPadM), 0f, 0.10f);
        stepUpHoldS = Mathf.Clamp(GetFloat("-walkStepUpHold", stepUpHoldS), 0f, 1.0f);
        stepUpPastM = Mathf.Max(0f, GetFloat("-walkStepUpPast", stepUpPastM));
        stepUpLeadM = Mathf.Max(-0.30f, GetFloat("-walkStepUpLead", stepUpLeadM));
        stepUpSpeedMps = Mathf.Max(0f, GetFloat("-walkStepUpSpeed", stepUpSpeedMps));
        stepUpHipShare = Mathf.Clamp(GetFloat("-walkStepUpHip", stepUpHipShare), 0f, 2f);
        stepUpAnkleShare = Mathf.Clamp01(GetFloat("-walkStepUpAnkle", stepUpAnkleShare));
        cadenceScale = Mathf.Clamp(GetFloat("-walkStepCadence", cadenceScale), 0.35f, 1f);
        cadenceVelMps = Mathf.Max(0f, GetFloat("-walkStepCadenceV", cadenceVelMps));
        cadenceBurstS = Mathf.Clamp(GetFloat("-walkStepCadenceS", cadenceBurstS), 0f, 5f);
        bandScale = Mathf.Clamp(GetFloat("-walkStepBand", bandScale), 1f, 2.5f);
        bandVelMps = Mathf.Max(0f, GetFloat("-walkStepBandV", bandVelMps));
        catchShare = Mathf.Clamp(GetFloat("-walkStepCatch", catchShare), 0.5f, 1.2f);
        brakeVelMps = Mathf.Max(0f, GetFloat("-walkStepCatchV", brakeVelMps));
        torsoLeanDeg = Mathf.Clamp(GetFloat("-walkStepLean", torsoLeanDeg), -20f, 20f);
        leanVelMps = Mathf.Max(0f, GetFloat("-walkStepLeanV", leanVelMps));
        stumbleVelMps = Mathf.Max(0f, GetFloat("-walkStumbleV", stumbleVelMps));
        stumbleHoldS = Mathf.Clamp(GetFloat("-walkStumbleHold", stumbleHoldS), 0f, 0.50f);
        logTrace = GetFloat("-walkLog", 0f) > 0.5f;
    }

    void Start()
    {
        pose = GetComponent<PoseDriveController>();
        if (pose == null)
        {
            Debug.LogWarning("WALKREFLEX: no PoseDriveController on " + name + " - inert");
            enabled = false;
            return;
        }
        pose.ReflexStumbleVelMps = stumble ? stumbleVelMps : 0f;
        pose.ReflexStumbleHoldS = stumble ? stumbleHoldS : 0f;
        Debug.Log("WALKREFLEX armed stepUp=" + (stepUp ? 1 : 0) + " stumble=" + (stumble ? 1 : 0)
            + " look=" + lookAheadM.ToString("0.00", CultureInfo.InvariantCulture)
            + " rate=" + riseRateMps.ToString("0.00", CultureInfo.InvariantCulture)
            + " lift=" + liftBoostM.ToString("0.000", CultureInfo.InvariantCulture)
            + " aim=" + (aimPlacement ? 1 : 0) + " dual=" + (dualRamp ? 1 : 0)
            + " stumbleV=" + stumbleVelMps.ToString("0.00", CultureInfo.InvariantCulture));
    }

    // Runs before StepPhaseDriver (-50) and the pose walk: the datum the walk
    // asks with on this physics step is the one read here.
    void FixedUpdate()
    {
        if (pose == null) return;
        if (!stepUp)
        {
            floorOffsetM = 0f;
            pose.ReflexFloorOffsetM = 0f;
            pose.ReflexLiftBoostM = 0f;
            return;
        }
        if (!pose.Active) return;

        float datum = pose.GroundYDatum;
        // The probe's seat: the pelvis plus a fixed look-ahead, or (aimPlacement)
        // the toe of the landing the law has already resolved. The placement is 0
        // until the walk has planned its first step, so the look-ahead answers then.
        float probeX = aimPlacement && pose.ReflexPlaceX > 0.01f
            ? pose.ReflexPlaceX + aimToeM
            : pose.ReflexEyeX + lookAheadM;
        Vector2 origin = new Vector2(probeX, datum + 0.80f);
        RaycastHit2D hit = Physics2D.Raycast(origin, Vector2.down, 2.0f, GroundLayers.Mask);
        // Only a hit near the pose's own ground datum is a floor reading: before the
        // walk's stand frame the datum is 0 and a ray from there sees nothing (and a
        // fancier acceptance test is not needed - every support surface this reflex
        // runs on is within a fraction of a metre of the walk's own plane).
        if (hit.collider != null && Mathf.Abs(hit.point.y - datum) < 0.60f)
            lastFloorY = hit.point.y;
        // The REFERENCE floor is the floor the walk STARTED on - probed, not the
        // pose's own datum: the datum (standPelvisY - pelvisCenterToSole) sits
        // 2.2 cm ABOVE the collider surface on this rig (measured 27.09: datum
        // -1.978 against the Spawn slab's -2.000), so an offset read against the
        // datum would carry that bias into every step. The latch waits for the
        // first VALID probe - latched against the datum's own 0 it pinned the
        // offset to its -9 cm floor for a whole run (27.09, run wrefdbg).
        if (float.IsNaN(floorRefY) && !float.IsNaN(lastFloorY))
            floorRefY = lastFloorY;
        float floorY = float.IsNaN(lastFloorY) ? datum : lastFloorY;

        float target = float.IsNaN(floorRefY)
            ? 0f : Mathf.Clamp(floorY - floorRefY, -riseMaxM, riseMaxM);
        // The ramp's own discipline (dualRamp): the rise is followed while both
        // soles are down; in single support it runs at singleRateMps (0 = the
        // offset holds until the incoming sole lands).
        float rate = dualRamp && !pose.ReflexBothSolesDown ? singleRateMps : riseRateMps;
        floorOffsetM = Mathf.MoveTowards(floorOffsetM, target, rate * Time.fixedDeltaTime);

        // While the rise is still AHEAD of the applied datum the sole gets the
        // extra clearance; once the walk is on the new floor the term is 0.
        float rise = Mathf.Max(0f, floorY - (floorRefY + floorOffsetM));
        pose.ReflexFloorOffsetM = floorOffsetM;
        pose.ReflexLiftBoostM = liftBoostM * Mathf.Clamp01(rise / riseFullM);

        // The riser's own X (the flick hold's trigger). While the probed floor is
        // still ABOVE the walk's own datum the floor ahead steps up; a short
        // forward ray from just in front of the pelvis, at just above the LOWER
        // plane, lands on the riser's face - the x an incoming sole must not be
        // sent under. On the rise (rise 0) nothing is re-read and a step DOWN
        // clears it.
        if (rise > 0.005f)
        {
            RaycastHit2D face = Physics2D.Raycast(
                new Vector2(pose.ReflexEyeX - 0.10f, floorRefY + 0.02f), Vector2.right, 1.6f, GroundLayers.Mask);
            if (face.collider != null) riserEdgeX = face.point.x;
        }
        else if (floorY < floorRefY - 0.005f)
        {
            riserEdgeX = float.NaN;
        }

        // The flick hold: a sole standing AT the riser keeps the stance flick
        // unamplified (PoseDriveController.ReflexHoldPush); the hold survives
        // flickHoldS after the last such frame.
        bool flickHold = false;
        if (flickGate && !float.IsNaN(riserEdgeX))
        {
            bool atRiser = (pose.ReflexLeftDown && Mathf.Abs(pose.ReflexLeftFootX - riserEdgeX) < flickZoneM)
                || (pose.ReflexRightDown && Mathf.Abs(pose.ReflexRightFootX - riserEdgeX) < flickZoneM);
            flickHoldT = atRiser ? flickHoldS : Mathf.Max(0f, flickHoldT - Time.fixedDeltaTime);
            flickHold = flickHoldT > 0f;
        }
        else
        {
            flickHoldT = 0f;
        }
        pose.ReflexHoldPush = flickHold;

        // The step-up delivery (v2): while a rise is still ahead (its riser known
        // and the body not yet past it), a leg whose sole stands ON the top surface
        // gets the climb's own delivery factor. The sole must be UP there first -
        // the low-plane sole of a trailing leg gets nothing. The hold survives
        // stepUpHoldS after the sole leaves the top, so a one-frame contact flicker
        // does not drop the push mid-climb.
        const float SoleHalfM = 0.045f;   // footSize.y 0.09, the rig's own number
        // The pelvis's own speed (m/s), smoothed: the assist's own trigger. The
        // climb drains the walk's pace long before the wall is in reach, so the
        // delivery follows the PACE, not the geometry - it fires while the walk is
        // slow at a rise and keeps firing past the edge until the pace is back.
        float eyeSpeed = (pose.ReflexEyeX - lastEyeX) / Mathf.Max(1e-5f, Time.fixedDeltaTime);
        eyeSpeedEma = eyeSpeedEma + (eyeSpeed - eyeSpeedEma) * Mathf.Clamp01(Time.fixedDeltaTime / 0.10f);
        lastEyeX = pose.ReflexEyeX;
        bool paceLive = stepUpSpeedMps <= 0f || eyeSpeedEma < stepUpSpeedMps;
        // WALK REFLEX v2c/v2d (28.09): the cadence law's own trigger - the walk's
        // clock runs faster while the walk outruns the cadence it was tuned at. 1
        // unless the flag is armed and the pace is above it, so the landed walk is
        // inert. With a burst length the faster clock is a burst per crossing (the
        // arm/disarm hysteresis below): the catch has to be transient, because the
        // walk's placement law follows the pace and a sustained fast clock makes the
        // catch run away.
        if (cadenceVelMps > 0f)
        {
            if (eyeSpeedEma < cadenceVelMps) cadenceArmed = true;
            else if (cadenceArmed && cadenceBurstS > 0f)
            {
                cadenceArmed = false;
                cadenceBurstT = cadenceBurstS;
            }
            cadenceBurstT = Mathf.Max(0f, cadenceBurstT - Time.fixedDeltaTime);
        }
        bool cadenceLive = cadenceVelMps > 0f && (cadenceBurstS > 0f
            ? cadenceBurstT > 0f : eyeSpeedEma > cadenceVelMps);
        pose.ReflexCadenceScale = cadenceLive ? cadenceScale : 1f;
        // WALK REFLEX v2e: the placement's own two pace seats (see their fields).
        pose.ReflexBandScale = bandVelMps > 0f && eyeSpeedEma > bandVelMps ? bandScale : 1f;
        pose.ReflexCatchShare = brakeVelMps > 0f && eyeSpeedEma > brakeVelMps ? catchShare : 0.5f;
        // WALK REFLEX v2f: the uprightness seat (see its field). The trunk lean is
        // the CoM lever - it asks the body's mass back while the pace is high.
        pose.ReflexTorsoLeanDeg = leanVelMps > 0f && eyeSpeedEma > leanVelMps ? torsoLeanDeg : 0f;
        float stepL = 0f, stepR = 0f;
        if (stepUpGain > 0f && paceLive && !float.IsNaN(riserEdgeX) && pose.ReflexEyeX < riserEdgeX + stepUpPastM)
        {
            float topSoleY = floorY - stepUpPadM;
            // The walk's own lead rule (BalanceController's stance-push-on-lead): a
            // push delivered while the CoM is still BEHIND the foot rotates the body
            // about the contact - a topple, not a climb. The delivery waits for the
            // pelvis to reach the foot.
            bool leadL = pose.ReflexEyeX >= pose.ReflexLeftFootX + stepUpLeadM;
            bool leadR = pose.ReflexEyeX >= pose.ReflexRightFootX + stepUpLeadM;
            bool onTopL = pose.ReflexLeftDown && leadL && pose.ReflexLeftFootY - SoleHalfM >= topSoleY;
            bool onTopR = pose.ReflexRightDown && leadR && pose.ReflexRightFootY - SoleHalfM >= topSoleY;
            stepUpHoldL = onTopL ? stepUpHoldS : Mathf.Max(0f, stepUpHoldL - Time.fixedDeltaTime);
            stepUpHoldR = onTopR ? stepUpHoldS : Mathf.Max(0f, stepUpHoldR - Time.fixedDeltaTime);
            // hold 0 means the instantaneous truth only: the sole is on the top.
            stepL = (onTopL || stepUpHoldL > 0f) ? stepUpGain : 0f;
            stepR = (onTopR || stepUpHoldR > 0f) ? stepUpGain : 0f;
        }
        else
        {
            stepUpHoldL = stepUpHoldR = 0f;
        }
        pose.ReflexStepUpL = stepL;
        pose.ReflexStepUpR = stepR;
        pose.ReflexStepUpHipShare = stepUpHipShare;
        pose.ReflexStepUpAnkle = stepUpAnkleShare;

        // Diagnostic line (-walkLog 1): what the probe saw, what the walk is
        // asking. One line every 0.25 s; read in the trial's stdout.
        logClock -= Time.fixedDeltaTime;
        if (logTrace && logClock <= 0f)
        {
            logClock = 0.25f;
            Debug.Log(string.Format(CultureInfo.InvariantCulture,
                "WALKREFLEX t={0:F2} px={1:F3} probeX={2:F3} floorY={3:F4} ref={4:F4} off={5:F4} boost={6:F4} placeX={7:F3} stumble={8} dL={9} dR={10} yL={11:F3} yR={12:F3} rise={13:F4} hold={14} su={15:F2}/{16:F2} fx={17:F3}/{18:F3} v={19:F2}",
                Time.time, pose.ReflexEyeX, probeX, floorY, floorRefY, floorOffsetM,
                pose.ReflexLiftBoostM, pose.ReflexPlaceX, pose.ReflexStumbleSteps,
                pose.ReflexLeftDown ? 1 : 0, pose.ReflexRightDown ? 1 : 0,
                pose.ReflexLeftFootY, pose.ReflexRightFootY, rise, flickHold ? 1 : 0,
                pose.ReflexStepUpL, pose.ReflexStepUpR,
                pose.ReflexLeftFootX, pose.ReflexRightFootX, eyeSpeedEma));
        }
    }

    private static float GetFloat(string name, float fallback)
    {
        string[] args = LauncherArgs.Args();
        int index = System.Array.IndexOf(args, ArgsAudit.Note(name));
        if (index < 0 || index + 1 >= args.Length) return fallback;
        return float.TryParse(args[index + 1], NumberStyles.Float,
            CultureInfo.InvariantCulture, out float value) ? value : fallback;
    }
}
