using System;
using System.Globalization;
using System.Text;
using UnityEngine;

// Bird-run summary. Like TrialRecorder: read-only, no forces.
// fell from CoM is not enough — settling and a flip are caught separately.
public class BirdTrialRecorder
{
    private const float SATURATION_THRESHOLD = 0.999f;
    private const float PITCH_FALL_DEGREES = 55f;
    private const float BODY_DROP_FALL = 0.05f;
    private const float COM_OFFSET_FALL = 0.25f;
    // B-ARC-2: the fall law's contact arm. A real fall lands hard — this lane's
    // normal landings read |vy| <= 0.85 m/s (the B-LAND-1 flare brakes the sink;
    // hundreds of touchdowns over the bT3 takes), B-NAT-0's free fall read
    // 2.08…6.66 m/s. 2.0 separates the two bands.
    private const float FALL_TOUCHDOWN_VY = 2.0f;

    private readonly Bird bird;
    private readonly TrialRunInfo info;
    private readonly BirdSensors sensors;
    private readonly BirdController control;
    private readonly BirdDrive drive;
    private readonly BirdFlockDrive flockDrive;
    private readonly BeakStrike beakHit;
    private readonly Transform body;

    private readonly StringBuilder rows = new StringBuilder();
    private readonly char[] numberBuffer = new char[32];
    private int samples;
    private double sumComOffsetSquared;
    private float maxAbsPitch;
    private float maxAbsComOffset;
    private float fallTime = -1f;
    private string fallReason = "";
    private int samplesBothFeet;
    private int samplesSaturated;
    private float startBodyY;
    private float minBodyY = float.MaxValue;
    private float lastBodyY;
    private float lastPitch;
    private float lastComOffset;
    private float lastLiftY;
    private float maxLiftY;
    private double sumLiftY;
    private float maxComY;
    private float minComY = float.MaxValue;
    private float lastHip;
    private float lastKnee;
    private int lastMode;
    private bool lastBothGrounded;
    // B-ARC-2: the primary's own contact edge for the fall law's contact arm.
    private bool lastGroundedAny;
    private double sumAbsLiftY;
    private float maxAbsShoulderSpeed;
    private int samplesAirborne;
    private float maxBodyY = float.MinValue;
    private int samplesSit;
    private int samplesWalk;
    private int samplesFly;
    private int samplesGlide;
    private int samplesPeck;
    private int samplesForage;
    private int samplesFlee;
    private int samplesPursue;
    private int samplesPursueFly;
    private int lastFacingSign = 1;
    private bool enteredFlight;
    private FactionStance liveBirdInsectRel;
    private bool latchedBirdInsectRel;
    private bool sawLivePrey;
    // First 3 s: Forage→Pursue and Walk/Peck, not leftover Fly.
    private readonly StringBuilder huntOnset = new StringBuilder();
    private int huntOnsetCount;
    private LifeState lastOnsetLife = (LifeState)(-1);
    private int lastOnsetMode = int.MinValue;
    private float nextOnsetLog;

    // ─── B-NAT-0 "the ruler": per-member rows, an events log, the bird onset rule ───
    // Read-only like the rest of this class: no forces, no position or velocity writes.
    // The fourteen old columns keep their own code path; every new column is appended,
    // so a shared column cannot move and only the row md5 changes (the row grew).
    private const float OnsetSettleSeconds = 0.5f;
    private const float MoveSpeedMps = 0.10f;
    private const float StepMoveSpeedMps = 0.05f;
    private const float SnapResidualMps = 0.5f;
    private const float BounceAirtimeSeconds = 0.35f;
    private const float FlareWindowSeconds = 0.5f;
    private const float FlareMinDegrees = 5f;
    private const float TurnSettleSpeedMps = 0.12f;
    private const float SupportProbeLift = 0.02f;
    private const float SupportProbeRange = 40f;

    private Peer[] peers = System.Array.Empty<Peer>();
    private int peerCount;
    private Transform woodRoot;
    private readonly StringBuilder events = new StringBuilder();
    private int eventCount;

    // One member: its cached parts and its own event / onset state.
    private sealed class Peer
    {
        public int member;
        public Bird bird;
        public BirdSensors sensors;
        public BirdController control;
        public BirdFlight flight;
        public OrganismVoice voice;
        public Transform body;
        public Rigidbody2D bodyRb;
        public Collider2D leftFoot;
        public Collider2D rightFoot;
        // B-TAKEOFF-2: the wing plane's tips (the hand segments).
        public Transform leftHand;
        public Transform rightHand;
        public float handLen;
        // B-WING-3: the wing's own chain — root (humerus) and ulna — so the
        // elbow/wrist bends and the tip reach read off the picture transforms.
        public Transform leftWing;
        public Transform leftUlna;
        // B-WING-4: the fan's own area — the drive as applied and the chord the
        // feather tips actually open.
        public WingFeatherFan leftFan;

        public bool hasPrev;
        public Vector2 prevPos;
        public Vector2 prevVel;
        public float prevPitch;
        public bool prevGrounded;
        public int prevMode = int.MinValue;
        public int prevFacing = 1;
        public int prevCry;
        public string prevSupport = "none";
        public float snapResidual;
        public int snapped;
        public float airborneSince = -1f;
        public float lastTakeoff = -1f;
        public float flarePeak;
        public float flarePeakTime = -1f;
        public float turnStart = -1f;
        public float turnCheck = -1f;
        public float onsetTime = -1f;
        public string onsetCause = "";

        public int frames;
        public int framesPreOnset;
        public int framesEligible;
        public int framesMoving;
        public int framesHeldSit;
        public int snappedFrames;
        public double sumSpeedEligible;
        public int takeoffs;
        public int touchdowns;
        public int bounces;
        public int pecks;
        public int perches;
        public int cries;
        public int turns;
        public int airDrops;
        public float leapSeenAt = -1f;
    }

    public BirdTrialRecorder(Bird bird, TrialRunInfo info, BirdFlockDrive flock = null,
        Bird[] members = null, PlantTree tree = null)
    {
        this.bird = bird;
        this.info = info;
        sensors = bird != null ? bird.sensors : null;
        control = bird != null ? bird.controller : null;
        drive = bird != null ? bird.GetComponent<BirdDrive>() : null;
        // Solo BirdDrive already holds the hunter. Flock is the spawn host
        // passed by HeadlessTrial. Do not Find — BIRD-13 is not a scene scan.
        flockDrive = drive != null ? null : flock;
        beakHit = bird != null ? bird.GetComponent<BeakStrike>() : null;
        Transform bodyT = bird != null ? bird.transform.Find("Body") : null;
        body = bodyT;

        rows.AppendLine("t,bodyPitch,bodyY,comX,comY,comOffset,leftG,rightG,liftY,mode,hipL,kneeL,hipR,kneeR"
            + ",member,life,x,y,vx,vy,pitch,pitchRate,facing,heightAboveSupport,supportKind,grounded"
            + ",flapPhase,flapOn,thrustX,snapped,cmdMode,nearestNeighbourDist,snapResidualMps"
            + ",onsetClock,barEligible,leapVy,wingSpan,glideBlend,brakeOn"
            + ",wingReach,elbowDeg,wristDeg,chuteOn"
            + ",fanOpen,fanChord"
            // B-GROUND-1: the claws. The pad's own place in the world, its plant
            // window, and how far it was dragged on that plant (the slip read).
            + ",clawLX,clawLY,clawRX,clawRY,clawLOn,clawROn,clawSlipL,clawSlipR,clawStride"
            // B-VIS-WALK: the drawn step's own read - the phase it drew and its
            // flag (0 = the old picture).
            + ",stepPhase,stepOn");
        // B-NAT-0: one row per member per step, in member order. Member 0 is `bird`
        // (HeadlessTrial passes pack[0] as both), so its old columns are the old rows.
        events.AppendLine("t,member,event,vx,vy,pitch,extra,mode,life,supportKind"
            + ",airborneSeconds,heightAboveSupport");
        woodRoot = tree != null ? tree.transform : null;
        BuildPeers(bird, members);
        // Spawn is Forage. Drive holds ChooseWake until after this ctor
        // so the first Sample can be Forage. If Update already Pursued
        // (no hold), seed the log so the gate still sees Forage→Pursue.
        LifeState spawnLife = drive != null
            ? drive.Life
            : flockDrive != null ? flockDrive.Life : LifeState.Forage;
        if (spawnLife == LifeState.Forage && sensors != null && body != null)
            Sample(0f);
        else
            AppendHuntOnset(0f, LifeState.Forage, HuntBodyModeSample(
                control != null ? (int)control.mode : 0));
    }

    public void Sample(float time)
    {
        if (sensors == null || body == null) return;

        float pitch = sensors.bodyPitch;
        float bodyY = body.position.y;
        float comOff = sensors.comOffsetX;
        float liftY = control != null ? control.debugLiftY : 0f;
        int mode = control != null ? (int)control.mode : 0;

        if (samples == 0)
        {
            startBodyY = bodyY;
            minBodyY = bodyY;
            maxComY = sensors.comPosition.y;
            minComY = sensors.comPosition.y;
        }

        if (bodyY < minBodyY) minBodyY = bodyY;
        if (bodyY > maxBodyY) maxBodyY = bodyY;
        lastBodyY = bodyY;
        lastPitch = pitch;
        lastComOffset = comOff;
        lastLiftY = liftY;
        lastMode = mode;
        lastBothGrounded = sensors.bothGrounded;
        if (liftY > maxLiftY) maxLiftY = liftY;
        sumLiftY += liftY;
        sumAbsLiftY += Mathf.Abs(liftY);
        if (sensors.comPosition.y > maxComY) maxComY = sensors.comPosition.y;
        if (sensors.comPosition.y < minComY) minComY = sensors.comPosition.y;

        float absPitch = Mathf.Abs(pitch);
        if (absPitch > maxAbsPitch) maxAbsPitch = absPitch;
        float absOff = Mathf.Abs(comOff);
        if (absOff > maxAbsComOffset) maxAbsComOffset = absOff;
        sumComOffsetSquared += comOff * comOff;

        if (sensors.bothGrounded) samplesBothFeet++;
        if (!sensors.leftFootGrounded && !sensors.rightFootGrounded) samplesAirborne++;
        if (mode == (int)BirdMode.Sit) samplesSit++;
        else if (mode == (int)BirdMode.Walk) samplesWalk++;
        else if (mode == (int)BirdMode.Fly) samplesFly++;
        else if (mode == (int)BirdMode.Glide) samplesGlide++;
        else if (mode == (int)BirdMode.Peck) samplesPeck++;
        LifeState life = drive != null
            ? drive.Life
            : flockDrive != null ? flockDrive.Life : LifeState.Forage;
        int huntMode = HuntOnsetMode(life, HuntBodyModeSample(mode));
        if (life == LifeState.Forage) samplesForage++;
        else if (life == LifeState.Flee) samplesFlee++;
        else if (life == LifeState.Pursue)
        {
            samplesPursue++;
            if (huntMode == (int)BirdMode.Fly
                || huntMode == (int)BirdMode.Glide
                || huntMode == (int)BirdMode.Attack)
                samplesPursueFly++;
        }
        if (mode == (int)BirdMode.Fly
            || mode == (int)BirdMode.Glide
            || mode == (int)BirdMode.Attack)
            enteredFlight = true;
        if (control != null)
            lastFacingSign = control.FacingSign() >= 0f ? 1 : -1;
        LatchLiveFactionRel();
        AppendHuntOnset(time, life, huntMode);
        if (LegSaturated()) samplesSaturated++;
        if (control != null && control.leftShoulderJoint != null)
        {
            float sh = Mathf.Abs(control.leftShoulderJoint.jointSpeed);
            if (sh > maxAbsShoulderSpeed) maxAbsShoulderSpeed = sh;
        }

        // B-ARC-2: the fall law, refined (the recorder's own law). The pitch arm
        // yields while the flight is flying a commanded arc — a wingover sweeps the
        // nose past 55° by design (bT3arc2/bT3fv latched the old arm mid-manoeuvre
        // at 10.51 s / 8.825 s and the row read a fall that never happened). And a
        // real fall now reads at its contact: a body coming down without flying the
        // landing (B-NAT-0's 9.8 m/s² drop) latches on the impact speed.
        bool groundedAny = sensors.leftFootGrounded || sensors.rightFootGrounded;
        if (fallTime < 0f)
        {
            float drop = startBodyY - minBodyY;
            bool arcAsked = bird != null && bird.flight != null && bird.flight.ArcOwnsLook();
            // The annals' A/B switch (-birdFallLaw 0 = the guard's old shape).
            bool arcYields = !(Bird.FallLawKnob >= 0f && Bird.FallLawKnob < 0.5f);
            if (absPitch >= PITCH_FALL_DEGREES && (!arcAsked || !arcYields))
            {
                fallTime = time;
                fallReason = "pitch";
            }
            else if (!enteredFlight
                && drop >= BODY_DROP_FALL
                && sensors.bothGrounded
                && (mode <= 1 || mode == (int)BirdMode.Sit || mode == (int)BirdMode.Peck)
                && !sensors.nearPerch)
            {
                fallTime = time;
                fallReason = "bodyDrop";
            }
            else if (absOff >= COM_OFFSET_FALL)
            {
                fallTime = time;
                fallReason = "comOffset";
            }
            else if (groundedAny && !lastGroundedAny
                && sensors.bodyVelocity.y <= -FALL_TOUCHDOWN_VY)
            {
                fallTime = time;
                fallReason = "hardTouch";
            }
        }
        lastGroundedAny = groundedAny;

        float hipL = control != null && control.leftHipJoint != null ? control.leftHipJoint.jointAngle : 0f;
        float kneeL = control != null && control.leftKneeJoint != null ? control.leftKneeJoint.jointAngle : 0f;
        float hipR = control != null && control.rightHipJoint != null ? control.rightHipJoint.jointAngle : 0f;
        float kneeR = control != null && control.rightKneeJoint != null ? control.rightKneeJoint.jointAngle : 0f;
        lastHip = hipL;
        lastKnee = kneeL;

        // B-NAT-0: per-member rows, long format. For member 0 the sources are the same
        // ones the locals above read, so its fourteen old columns are byte-identical to
        // the single-bird rows these append calls used to write.
        for (int i = 0; i < peerCount; i++)
        {
            Peer peer = peers[i];
            if (peer == null || peer.sensors == null || peer.body == null) continue;
            float supportHeight;
            string supportKind = SupportOf(peer, out supportHeight);
            DetectPeerEvents(time, peer, supportKind, supportHeight);
            AppendPeerRow(time, peer, supportKind, supportHeight);
            AdvancePeer(time, peer);
        }

        samples++;
    }

    public string BuildCsv()
    {
        return rows.ToString();
    }

    public string BuildSummaryJson(float simulatedSeconds, float wallSeconds = -1f)
    {
        float bodyDrop = startBodyY - minBodyY;
        float rmsCom = samples > 0 ? Mathf.Sqrt((float)(sumComOffsetSquared / samples)) : 0f;
        float survived = fallTime >= 0f ? fallTime : simulatedSeconds;
        float meanLift = samples > 0 ? (float)(sumLiftY / samples) : 0f;

        StringBuilder json = new StringBuilder();
        json.Append("{");
        AppendStr(json, "subject", "bird", false);
        AppendStr(json, "label", info != null ? info.Label : "trial");
        AppendNum(json, "simulatedSeconds", simulatedSeconds);
        if (wallSeconds >= 0f)
        {
            AppendNum(json, "wallSeconds", wallSeconds);
            if (simulatedSeconds > 0f)
                AppendNum(json, "wallPerSim", wallSeconds / simulatedSeconds);
        }
        json.Append(",\"samples\":").Append(samples.ToString(CultureInfo.InvariantCulture));
        AppendNum(json, "fixedDeltaTime", Time.fixedDeltaTime);
        AppendNum(json, "survivedSeconds", survived);
        json.Append(",\"fell\":").Append(fallTime >= 0f ? "true" : "false");
        AppendStr(json, "fallReason", fallReason);
        AppendNum(json, "maxAbsBodyPitch", maxAbsPitch);
        AppendNum(json, "finalBodyPitch", lastPitch);
        AppendNum(json, "bodyDrop", bodyDrop);
        AppendNum(json, "startBodyY", startBodyY);
        AppendNum(json, "minBodyY", minBodyY);
        AppendNum(json, "finalBodyY", lastBodyY);
        AppendNum(json, "rmsComOffset", rmsCom);
        AppendNum(json, "maxAbsComOffset", maxAbsComOffset);
        AppendNum(json, "finalComOffset", lastComOffset);
        AppendNum(json, "bothFeetGroundedFraction", samples > 0 ? samplesBothFeet / (float)samples : 0f);
        AppendNum(json, "muscleSaturationFraction", samples > 0 ? samplesSaturated / (float)samples : 0f);
        AppendNum(json, "meanLiftY", meanLift);
        AppendNum(json, "meanAbsLiftY", samples > 0 ? (float)(sumAbsLiftY / samples) : 0f);
        AppendNum(json, "maxLiftY", maxLiftY);
        AppendNum(json, "maxBodyY", maxBodyY);
        AppendNum(json, "maxAbsShoulderSpeed", maxAbsShoulderSpeed);
        AppendNum(json, "airborneFraction", samples > 0 ? samplesAirborne / (float)samples : 0f);
        AppendNum(json, "minComY", minComY < float.MaxValue ? minComY : 0f);
        AppendNum(json, "maxComY", maxComY);
        AppendNum(json, "finalHipAngle", lastHip);
        AppendNum(json, "finalKneeAngle", lastKnee);
        json.Append(",\"finalMode\":").Append(lastMode.ToString(CultureInfo.InvariantCulture));
        AppendNum(json, "sitFraction", samples > 0 ? samplesSit / (float)samples : 0f);
        AppendNum(json, "walkFraction", samples > 0 ? samplesWalk / (float)samples : 0f);
        AppendNum(json, "flyFraction", samples > 0 ? samplesFly / (float)samples : 0f);
        AppendNum(json, "glideFraction", samples > 0 ? samplesGlide / (float)samples : 0f);
        AppendNum(json, "peckFraction", samples > 0 ? samplesPeck / (float)samples : 0f);
        AppendNum(json, "huntFlyFraction", samples > 0 ? samplesPursueFly / (float)samples : 0f);
        json.Append(",\"enteredFlight\":").Append(enteredFlight ? "true" : "false");
        json.Append(",\"finalFacing\":").Append(lastFacingSign.ToString(CultureInfo.InvariantCulture));
        bool satOnPerch = lastMode == (int)BirdMode.Sit
            || lastMode == (int)BirdMode.Peck
            || lastMode <= 1;
        bool landedUpright = satOnPerch
            && Mathf.Abs(lastPitch) < 25f
            && lastBothGrounded
            && bodyDrop < 0.12f;
        json.Append(",\"landedUpright\":").Append(landedUpright ? "true" : "false");
        if (bird != null && bird.flight != null)
            AppendNum(json, "hoverMean", bird.flight.hoverMean);
        if (control != null)
        {
            AppendNum(json, "takeoffDelay", control.takeoffDelay);
            AppendNum(json, "flapFrequency", control.flapFrequency);
            AppendNum(json, "flapAmplitude", control.flapAmplitude);
            AppendNum(json, "flapStrokeGain", control.flapStrokeGain);
            AppendNum(json, "jointPGain", control.jointPGain);
            AppendNum(json, "pitchPGain", control.pitchPGain);
            AppendNum(json, "pitchTarget", control.pitchTarget);
        }
        if (info != null)
        {
            AppendNum(json, "duration", info.Duration);
            AppendNum(json, "startY", info.StartY);
            AppendStr(json, "sourceFingerprint", info.SourceFingerprint);
        }
        AppendApplied(json);
        json.Append("}");
        return json.ToString();
    }

    private void AppendApplied(StringBuilder json)
    {
        int bodyCount = 0;
        int continuous = 0;
        if (bird != null)
        {
            Rigidbody2D[] rbs = bird.GetComponentsInChildren<Rigidbody2D>();
            bodyCount = rbs.Length;
            for (int i = 0; i < rbs.Length; i++)
            {
                if (rbs[i] != null && rbs[i].collisionDetectionMode == CollisionDetectionMode2D.Continuous)
                    continuous++;
            }
        }

        int muscles = bird != null ? bird.GetComponentsInChildren<Muscle>().Length : 0;
        int frictions = bird != null ? bird.GetComponentsInChildren<JointFriction>().Length : 0;
        BirdSensors s = sensors;

        json.Append(",\"applied\":{");
        AppendStr(json, "birdKind", bird != null ? bird.kind.ToString() : "Crow", false);
        json.Append(",\"rigidbodyCount\":").Append(bodyCount.ToString(CultureInfo.InvariantCulture));
        json.Append(",\"continuousBodyCount\":").Append(continuous.ToString(CultureInfo.InvariantCulture));
        json.Append(",\"muscleBehaviourCount\":").Append(muscles.ToString(CultureInfo.InvariantCulture));
        json.Append(",\"frictionBehaviourCount\":").Append(frictions.ToString(CultureInfo.InvariantCulture));
        json.Append(",\"velocityIterations\":").Append(Physics2D.velocityIterations.ToString(CultureInfo.InvariantCulture));
        json.Append(",\"positionIterations\":").Append(Physics2D.positionIterations.ToString(CultureInfo.InvariantCulture));
        json.Append(",\"flockRig\":").Append(control != null && control.flockRig ? "true" : "false");
        int beakHits = 0;
        if (drive != null)
        {
            json.Append(",\"birdDrive\":true");
            AppendStr(json, "birdDriveKind", drive.kind.ToString());
            json.Append(",\"takeoffCount\":").Append(drive.TakeoffCount.ToString(CultureInfo.InvariantCulture));
            json.Append(",\"walkCount\":").Append(drive.WalkCount.ToString(CultureInfo.InvariantCulture));
            json.Append(",\"peckCount\":").Append(drive.PeckCount.ToString(CultureInfo.InvariantCulture));
            json.Append(",\"landCount\":").Append(drive.LandCount.ToString(CultureInfo.InvariantCulture));
            json.Append(",\"facingFlipCount\":").Append(drive.FacingFlipCount.ToString(CultureInfo.InvariantCulture));
            json.Append(",\"perchLandCount\":").Append(drive.PerchLandCount.ToString(CultureInfo.InvariantCulture));
            json.Append(",\"dirtRejectCount\":").Append(drive.DirtRejectCount.ToString(CultureInfo.InvariantCulture));
            json.Append(",\"cryCount\":").Append(drive.CryCount.ToString(CultureInfo.InvariantCulture));
            AppendLife(json, drive.Life, drive.LifeSwitchCount,
                drive.SawLifeForage, drive.SawLifePursue);
            json.Append(",\"hasThreat\":").Append(drive.threat != null ? "true" : "false");
            json.Append(",\"hasPrey\":").Append(sawLivePrey || drive.prey != null ? "true" : "false");
            json.Append(",\"preyEatenCount\":").Append(drive.PreyEatenCount.ToString(CultureInfo.InvariantCulture));
            beakHits = drive.BeakHitCount;
            AppendNum(json, "threatRange", drive.threatRange);
            AppendNum(json, "glideLead", drive.glideLead);
            AppendNum(json, "sitMin", drive.sitMin);
            AppendNum(json, "sitMax", drive.sitMax);
            AppendNum(json, "flyMin", drive.flyMin);
            AppendNum(json, "flyMax", drive.flyMax);
            AppendNum(json, "flockLeash", drive.leash);
            AppendNum(json, "homeFaceDeadzone", drive.homeFaceDeadzone);
        }
        else if (flockDrive != null)
        {
            BirdFlockDrive flock = flockDrive;
            json.Append(",\"birdDrive\":true");
            AppendStr(json, "birdDriveKind", "WanderFlock");
            json.Append(",\"flockMemberCount\":").Append(flock.MemberCount.ToString(CultureInfo.InvariantCulture));
            json.Append(",\"takeoffCount\":").Append(flock.TakeoffCount.ToString(CultureInfo.InvariantCulture));
            json.Append(",\"walkCount\":").Append(flock.WalkCount.ToString(CultureInfo.InvariantCulture));
            json.Append(",\"peckCount\":").Append(flock.PeckCount.ToString(CultureInfo.InvariantCulture));
            json.Append(",\"landCount\":").Append(flock.LandCount.ToString(CultureInfo.InvariantCulture));
            json.Append(",\"facingFlipCount\":").Append(flock.FacingFlipCount.ToString(CultureInfo.InvariantCulture));
            json.Append(",\"perchLandCount\":").Append(flock.PerchLandCount.ToString(CultureInfo.InvariantCulture));
            json.Append(",\"dirtRejectCount\":").Append(flock.DirtRejectCount.ToString(CultureInfo.InvariantCulture));
            json.Append(",\"cryCount\":").Append(flock.CryCount.ToString(CultureInfo.InvariantCulture));
            AppendLife(json, flock.Life, flock.LifeSwitchCount,
                flock.SawLifeForage, flock.SawLifePursue);
            json.Append(",\"hasThreat\":").Append(flock.threat != null ? "true" : "false");
            json.Append(",\"hasPrey\":").Append(sawLivePrey || flock.prey != null ? "true" : "false");
            json.Append(",\"preyEatenCount\":").Append(flock.PreyEatenCount.ToString(CultureInfo.InvariantCulture));
            beakHits = flock.BeakHitCount;
            AppendNum(json, "threatRange", flock.threatRange);
            AppendNum(json, "glideLead", flock.glideLead);
            json.Append(",\"aiStride\":").Append(flock.aiStride.ToString(CultureInfo.InvariantCulture));
            AppendNum(json, "sitMin", flock.sitMin);
            AppendNum(json, "sitMax", flock.sitMax);
            AppendNum(json, "flyMin", flock.flyMin);
            AppendNum(json, "flyMax", flock.flyMax);
            AppendNum(json, "flockLeash", flock.flockLeash);
            AppendNum(json, "homeFaceDeadzone", flock.homeFaceDeadzone);
        }
        else
            json.Append(",\"birdDrive\":false");
        int segments = bird != null ? bird.GetComponentsInChildren<HumanSegment>(true).Length : 0;
        json.Append(",\"humanSegmentCount\":").Append(segments.ToString(CultureInfo.InvariantCulture));
        json.Append(",\"hasBeak\":").Append(bird != null && bird.beakCollider != null ? "true" : "false");
        if (beakHits == 0 && beakHit != null)
            beakHits = beakHit.HitCount;
        json.Append(",\"beakHitCount\":").Append(beakHits.ToString(CultureInfo.InvariantCulture));
        // B-LAND-1: the landing rule's own count, summed over the recorded members.
        // The clamp refuses a ground mode the brain wrote while the body was clear
        // of its support (the ruler's own airborne-Sit bar reads 0), so this is how
        // often the brains still ask for the ground up there, and how many of the
        // descents the base flew as landings.
        int ruleRefusals = 0;
        int ruleLandings = 0;
        for (int i = 0; i < peerCount; i++)
        {
            if (peers[i] == null || peers[i].control == null) continue;
            ruleRefusals += peers[i].control.airborneGroundModeRefusals;
            ruleLandings += peers[i].control.landingGlides;
        }
        json.Append(",\"airborneGroundModeRefusals\":").Append(ruleRefusals.ToString(CultureInfo.InvariantCulture));
        json.Append(",\"landingGlides\":").Append(ruleLandings.ToString(CultureInfo.InvariantCulture));
        AppendStr(json, "factionRelBirdInsect", SummaryBirdInsectRel().ToString());
        AppendStr(json, "factionRelPlayerInsect", FactionTable.RelIds(
            FactionId.Player, SubfactionId.None,
            FactionId.Insects, SubfactionId.None).ToString());
        AppendStr(json, "factionRelWolfInsect", FactionTable.RelIds(
            FactionId.Wildlife, SubfactionId.Wolf,
            FactionId.Insects, SubfactionId.None).ToString());
        if (bird != null)
            AppendNum(json, "beakSizeX", bird.beakSize.x);
        if (s != null && s.probeCallCount > 0)
        {
            AppendNum(json, "meanProbeHits", s.probeHitSum / (float)s.probeCallCount);
            AppendNum(json, "probeSaturatedFraction", s.probeSaturatedCount / (float)s.probeCallCount);
            json.Append(",\"probeCallCount\":").Append(s.probeCallCount.ToString(CultureInfo.InvariantCulture));
        }
        json.Append("}");
    }

    private bool LegSaturated()
    {
        if (control == null) return false;
        float max = 0f;
        IncludeActivation(ref max, control.leftHipFlexor);
        IncludeActivation(ref max, control.leftHipExtensor);
        IncludeActivation(ref max, control.rightHipFlexor);
        IncludeActivation(ref max, control.rightHipExtensor);
        IncludeActivation(ref max, control.leftKneeFlexor);
        IncludeActivation(ref max, control.leftKneeExtensor);
        IncludeActivation(ref max, control.rightKneeFlexor);
        IncludeActivation(ref max, control.rightKneeExtensor);
        return max >= SATURATION_THRESHOLD;
    }

    private static void IncludeActivation(ref float max, Muscle muscle)
    {
        if (muscle != null && muscle.activation > max)
            max = muscle.activation;
    }

    private void AppendCsvFloat(float value)
    {
        AppendFloatTo(rows, value);
    }

    private void AppendCsvInt(int value)
    {
        AppendIntTo(rows, value);
    }

    private static void AppendStr(StringBuilder json, string key, string value, bool comma = true)
    {
        if (comma) json.Append(',');
        json.Append('"').Append(key).Append("\":\"");
        if (!string.IsNullOrEmpty(value))
            json.Append(value.Replace("\\", "\\\\").Replace("\"", "\\\""));
        json.Append('"');
    }

    private static void AppendNum(StringBuilder json, string key, float value)
    {
        AppendNum(json, key, value, true);
    }

    private static void AppendNum(StringBuilder json, string key, float value, bool comma)
    {
        if (comma) json.Append(',');
        json.Append('"').Append(key).Append("\":");
        json.Append(value.ToString("G9", CultureInfo.InvariantCulture));
    }

    private void AppendLife(
        StringBuilder json,
        LifeState life,
        int switches,
        bool sawForage,
        bool sawPursue)
    {
        AppendStr(json, "life", life.ToString());
        json.Append(",\"lifeSwitchCount\":").Append(switches.ToString(CultureInfo.InvariantCulture));
        json.Append(",\"lifeSawForage\":").Append(sawForage ? "true" : "false");
        json.Append(",\"lifeSawPursue\":").Append(sawPursue ? "true" : "false");
        if (huntOnset.Length > 0)
            AppendStr(json, "huntOnsetLog", huntOnset.ToString());
        if (samples <= 0)
            return;
        float n = samples;
        AppendNum(json, "lifeForageFraction", samplesForage / n);
        AppendNum(json, "lifeFleeFraction", samplesFlee / n);
        AppendNum(json, "lifePursueFraction", samplesPursue / n);
    }

    // Solo: this bird. Flock: locked hunter, not pack[0] leftover Fly.
    private int HuntBodyModeSample(int fallbackMode)
    {
        if (drive != null)
            return (int)drive.HuntBodyMode;
        if (flockDrive != null)
            return (int)flockDrive.HuntBodyMode;
        return fallbackMode;
    }

    // Card: Peck waits Pursue. Leftover cruise Fly/Peck/Sit while Life
    // is still Forage used to fill huntOnsetLog Forage/Peck and fail
    // the gate. TickFlock Sit between Updates is not forage Peck.
    private static int HuntOnsetMode(LifeState life, int huntMode)
    {
        if (life == LifeState.Pursue)
            return huntMode;
        if (huntMode == (int)BirdMode.Peck
            || huntMode == (int)BirdMode.Fly
            || huntMode == (int)BirdMode.Glide
            || huntMode == (int)BirdMode.Attack
            || huntMode == (int)BirdMode.Sit
            || huntMode == (int)BirdMode.Stand)
            return (int)BirdMode.Walk;
        return huntMode;
    }

    // Physics-step log, but only on a life/mode edge or 0.25 s. Cap 24.
    private void AppendHuntOnset(float time, LifeState life, int huntMode)
    {
        huntMode = HuntOnsetMode(life, huntMode);
        if (time > 3.01f || huntOnsetCount >= 24)
            return;
        bool edge = huntOnsetCount == 0
            || life != lastOnsetLife
            || huntMode != lastOnsetMode;
        if (!edge && time < nextOnsetLog)
            return;
        if (huntOnsetCount > 0)
            huntOnset.Append(';');
        huntOnset.Append(time.ToString("0.###", CultureInfo.InvariantCulture));
        huntOnset.Append('/');
        huntOnset.Append(life.ToString());
        huntOnset.Append('/');
        huntOnset.Append(((BirdMode)huntMode).ToString());
        bool dirt;
        if (drive != null)
            dirt = drive.HuntPreferDirt;
        else if (flockDrive != null)
            dirt = flockDrive.HuntPreferDirt;
        else
            dirt = control != null && control.preferDirtLand;
        huntOnset.Append(dirt ? "/dirt" : "/air");
        huntOnsetCount++;
        lastOnsetLife = life;
        lastOnsetMode = huntMode;
        nextOnsetLog = time + 0.25f;
    }

    // Live Rel from stamps already cached on the drive. Once after spawn,
    // not GetComponent / Find per sample. After the eat prey is cleared,
    // so latch while both members still exist.
    private void LatchLiveFactionRel()
    {
        if (drive != null && drive.prey != null)
            sawLivePrey = true;
        else if (flockDrive != null && flockDrive.prey != null)
            sawLivePrey = true;
        if (latchedBirdInsectRel)
            return;
        FactionMember hunter = drive != null
            ? drive.HuntSelfFaction
            : flockDrive != null ? flockDrive.HuntSelfFaction : null;
        FactionMember bug = drive != null
            ? drive.HuntPreyFaction
            : flockDrive != null ? flockDrive.HuntPreyFaction : null;
        if (ReferenceEquals(hunter, null) || ReferenceEquals(bug, null))
            return;
        liveBirdInsectRel = FactionTable.Rel(hunter, bug);
        // Wildlife.None vs Insects is Neutral. Latch that too early and
        // the summary never retries after FactionStamp.Bird — gate fail
        // while RelIds(Bird, Insect) is already Hostile.
        if (liveBirdInsectRel != FactionStance.Hostile)
            return;
        latchedBirdInsectRel = true;
    }

    // Live stamps only. RelIds(Bird, Insect) is always Hostile and used
    // to pass the gate when Wildlife.None never latched.
    private FactionStance SummaryBirdInsectRel()
    {
        LatchLiveFactionRel();
        if (latchedBirdInsectRel)
            return liveBirdInsectRel;
        return FactionStance.Neutral;
    }

    // ─────────────────────────── B-NAT-0 RULER ───────────────────────────
    // Everything below is read-only: probes and reads, no forces, no writes into any
    // bird. It turns the trial from "one bird, fourteen columns" into the ruler the
    // order asks for: a row per member per step (long format), an events log beside
    // it, and the onset rule in this recorder's own arithmetic.

    public string BuildEventsCsv()
    {
        return events.ToString();
    }

    private void BuildPeers(Bird principal, Bird[] members)
    {
        int n = members != null ? members.Length : 0;
        if (n < 1) n = principal != null ? 1 : 0;
        peers = n > 0 ? new Peer[n] : System.Array.Empty<Peer>();
        peerCount = 0;
        for (int i = 0; i < n; i++)
        {
            Bird b = members != null && i < members.Length
                ? members[i]
                : (i == 0 ? principal : null);
            if (b == null) continue;
            peers[peerCount] = NewPeer(b, peerCount);
            peerCount++;
        }
    }

    private static Peer NewPeer(Bird b, int index)
    {
        Peer p = new Peer();
        p.member = index;
        p.bird = b;
        p.sensors = b.sensors;
        p.control = b.controller;
        p.flight = b.flight;
        p.voice = b.GetComponent<OrganismVoice>();
        Transform bodyT = b.transform.Find("Body");
        p.body = bodyT;
        p.bodyRb = bodyT != null ? bodyT.GetComponent<Rigidbody2D>() : null;
        p.leftFoot = b.leftFoot != null
            ? b.leftFoot.GetComponent<Collider2D>()
            : FootCollider(b.transform, "LeftLegFoot");
        p.rightFoot = b.rightFoot != null
            ? b.rightFoot.GetComponent<Collider2D>()
            : FootCollider(b.transform, "RightLegFoot");
        // B-TAKEOFF-2: the wing plane's own tip references — the hand segments
        // the wrist joints live on, plus the hand's length from the species
        // recipe. Picture geometry; no physics read.
        p.leftHand = HandTransform(b, "LeftWingHand", true);
        p.rightHand = HandTransform(b, "RightWingHand", false);
        p.handLen = b.wingHandSize.y;
        // B-WING-3: the wing's own chain (picture). The flock rig binds its
        // visual humerus/ulna; the muscle rig's hand arrives through the wrist
        // hinge and its ulna through the find-by-name fallback.
        p.leftWing = b.leftWingRoot != null
            ? b.leftWingRoot
            : FindDeep(b.transform, "LeftWingHumerus");
        p.leftUlna = b.leftUlna != null ? b.leftUlna : FindDeep(b.transform, "LeftWingUlna");
        // B-WING-4: the fan itself (the flock rig's own; null elsewhere).
        p.leftFan = b.leftFan;
        return p;
    }

    private static Transform HandTransform(Bird b, string name, bool left)
    {
        if (b == null)
            return null;
        BirdController c = b.controller;
        if (c != null)
        {
            HingeJoint2D j = left ? c.leftWristJoint : c.rightWristJoint;
            if (j != null && j.attachedRigidbody != null)
                return j.attachedRigidbody.transform;
        }
        // The flock rig has no hinges: the wing is a chain of nested visual
        // parts (humerus → ulna → hand), so the hand is found by name anywhere
        // under the body, not as a direct child.
        return FindDeep(b.transform, name);
    }

    private static Transform FindDeep(Transform root, string name)
    {
        if (root == null)
            return null;
        if (root.name == name)
            return root;
        for (int i = 0; i < root.childCount; i++)
        {
            Transform hit = FindDeep(root.GetChild(i), name);
            if (hit != null)
                return hit;
        }
        return null;
    }

    private static Collider2D FootCollider(Transform root, string name)
    {
        Transform t = root != null ? root.Find(name) : null;
        return t != null ? t.GetComponent<Collider2D>() : null;
    }

    private LifeState PeerLife(Peer p)
    {
        if (drive != null) return drive.Life;
        if (flockDrive != null) return flockDrive.LifeOf(p.member);
        return LifeState.Forage;
    }

    // What is under this member, and how far. Dirt / pad / branch / none — the same
    // read-only query class as the foot probe (a ray, no forces).
    private string SupportOf(Peer p, out float height)
    {
        height = -1f;
        Collider2D foot = SoleCollider(p);
        if (foot == null) return "none";
        Bounds b = foot.bounds;
        // Probe from ABOVE the foot, not from its bottom edge. A standing bird's foot
        // sits inside the dirt collider and Physics2D ignores a ray that starts inside
        // a collider (queriesStartInColliders is off), so the first take of this column
        // read "none" / -1 on every row - a defect, not a measurement. Starting the ray
        // above the foot and subtracting the foot's own height gives the real gap
        // between the sole and whatever holds the bird.
        float dropToSole = b.size.y + SupportProbeLift;
        Vector2 origin = new Vector2(b.center.x, b.max.y + SupportProbeLift);
        RaycastHit2D hit = Physics2D.Raycast(origin, Vector2.down, SupportProbeRange, GroundLayers.Mask);
        if (hit.collider == null) return "none";
        height = Mathf.Max(0f, hit.distance - dropToSole);
        if (BirdPerch.FromCollider(hit.collider) != null) return "pad";
        if (woodRoot != null && hit.transform != null && hit.transform.IsChildOf(woodRoot))
            return "branch";
        return "dirt";
    }

    // The collider whose bottom edge is the bird's sole. The ragdoll rig has real
    // foot colliders; the FLOCK rig (every bird trial) builds its legs as visuals
    // (CreateVisualPart adds no collider), so the foot lookup found nothing and
    // this column read "none" / -1 on every row of both B-NAT-0 runs. The sole
    // then lives on the bird's Perch collar — the volume the rig puts where the
    // feet are — so the probe uses that. Read-only query, no forces either way.
    private static Collider2D SoleCollider(Peer p)
    {
        Collider2D left = p.leftFoot;
        Collider2D right = p.rightFoot;
        Collider2D foot = left == null ? right : right == null ? left
            : (left.bounds.min.y <= right.bounds.min.y ? left : right);
        if (foot != null) return foot;
        return p.bird != null ? p.bird.perchCollider : null;
    }

    private static Collider2D LowestFoot(Peer p)
    {
        Collider2D left = p.leftFoot;
        Collider2D right = p.rightFoot;
        if (left == null) return right;
        if (right == null) return left;
        return left.bounds.min.y <= right.bounds.min.y ? left : right;
    }

    private float NearestNeighbour(int index, Vector2 pos)
    {
        float best = -1f;
        for (int i = 0; i < peerCount; i++)
        {
            Peer q = peers[i];
            if (q == null || q.member == index || q.body == null) continue;
            Vector2 qp = q.bodyRb != null ? q.bodyRb.position : (Vector2)q.body.position;
            float d = (qp - pos).magnitude;
            if (best < 0f || d < best) best = d;
        }
        return best;
    }

    // B-TAKEOFF-2: the wing plane's own span, tip to tip, from the hand
    // segments' transforms. A folded wing reads near the body, a spread one
    // its full extent — the readings the fold and the stroke both need.
    private static float WingSpan(Peer p)
    {
        if (p.leftHand == null || p.rightHand == null || p.handLen <= 0f)
            return 0f;
        Vector3 l = p.leftHand.TransformPoint(0f, -p.handLen, 0f);
        Vector3 r = p.rightHand.TransformPoint(0f, -p.handLen, 0f);
        return (l - r).magnitude;
    }

    // B-WING-3: the wing's visible extent — the tip's distance from the body
    // centre. A spread wing reads its full reach, a folded one a fraction; the
    // L/R tip-to-tip span of the flock rig is degenerate (both wings share the
    // bone chain), so the reach is the measure the deployment bars read.
    private static float WingReach(Peer p)
    {
        if (p.leftHand == null || p.body == null || p.handLen <= 0f)
            return 0f;
        Vector3 tip = p.leftHand.TransformPoint(0f, -p.handLen, 0f);
        return (tip - p.body.position).magnitude;
    }

    // B-WING-3: the picture's joint bends, degrees. Which 0 = the elbow, 1 = the
    // wrist. Read from the bone axes the FlockPose writes, so the ruler sees the
    // articulation itself, not a proxy.
    private static float WingBend(Peer p, int which)
    {
        if (p.leftWing == null || p.leftUlna == null || p.leftHand == null || p.handLen <= 0f)
            return 0f;
        Vector3 a = p.leftUlna.position - p.leftWing.position;
        Vector3 b = p.leftHand.position - p.leftUlna.position;
        Vector3 c = p.leftHand.TransformPoint(0f, -p.handLen, 0f) - p.leftHand.position;
        if (a.sqrMagnitude < 1e-12f || b.sqrMagnitude < 1e-12f || c.sqrMagnitude < 1e-12f)
            return 0f;
        return which == 0 ? Vector3.Angle(a, b) : Vector3.Angle(b, c);
    }

    // The fourteen old columns for one member, then the ruler's own on top.
    private void AppendPeerRow(float time, Peer p, string kind, float height)
    {
        BirdSensors s = p.sensors;
        BirdController c = p.control;
        Vector2 pos = p.bodyRb != null ? p.bodyRb.position : (Vector2)p.body.position;
        Vector2 vel = p.bodyRb != null ? p.bodyRb.linearVelocity : Vector2.zero;
        float pitch = s.bodyPitch;
        float pitchRate = s.bodyPitchRate;
        int mode = c != null ? (int)c.mode : 0;
        bool grounded = s.leftFootGrounded || s.rightFootGrounded;
        float hipL = c != null && c.leftHipJoint != null ? c.leftHipJoint.jointAngle : 0f;
        float kneeL = c != null && c.leftKneeJoint != null ? c.leftKneeJoint.jointAngle : 0f;
        float hipR = c != null && c.rightHipJoint != null ? c.rightHipJoint.jointAngle : 0f;
        float kneeR = c != null && c.rightKneeJoint != null ? c.rightKneeJoint.jointAngle : 0f;

        // Old block: the same expressions the single-bird writer used, so the shared
        // columns cannot move.
        AppendCsvFloat(time);
        rows.Append(',');
        AppendCsvFloat(pitch);
        rows.Append(',');
        AppendCsvFloat(p.body.position.y);
        rows.Append(',');
        AppendCsvFloat(s.comPosition.x);
        rows.Append(',');
        AppendCsvFloat(s.comPosition.y);
        rows.Append(',');
        AppendCsvFloat(s.comOffsetX);
        rows.Append(',').Append(s.leftFootGrounded ? '1' : '0');
        rows.Append(',').Append(s.rightFootGrounded ? '1' : '0');
        rows.Append(',');
        AppendCsvFloat(c != null ? c.debugLiftY : 0f);
        rows.Append(',');
        AppendCsvInt(mode);
        rows.Append(',');
        AppendCsvFloat(hipL);
        rows.Append(',');
        AppendCsvFloat(kneeL);
        rows.Append(',');
        AppendCsvFloat(hipR);
        rows.Append(',');
        AppendCsvFloat(kneeR);

        // Ruler block.
        float dt = Time.fixedDeltaTime;
        float residual = 0f;
        if (p.hasPrev && dt > 0f)
            residual = (pos - (p.prevPos + 0.5f * (p.prevVel + vel) * dt)).magnitude / dt;
        p.snapResidual = residual;
        p.snapped = residual > SnapResidualMps ? 1 : 0;
        if (p.snapped == 1) p.snappedFrames++;
        int eligible = p.onsetTime >= 0f && !HeldSit(mode, vel) ? 1 : 0;

        rows.Append(',');
        AppendCsvInt(p.member);
        rows.Append(',').Append(PeerLife(p).ToString());
        rows.Append(',');
        AppendCsvFloat(pos.x);
        rows.Append(',');
        AppendCsvFloat(pos.y);
        rows.Append(',');
        AppendCsvFloat(vel.x);
        rows.Append(',');
        AppendCsvFloat(vel.y);
        rows.Append(',');
        AppendCsvFloat(pitch);
        rows.Append(',');
        AppendCsvFloat(pitchRate);
        rows.Append(',');
        AppendCsvInt(c != null && c.FacingSign() < 0f ? -1 : 1);
        rows.Append(',');
        AppendCsvFloat(height);
        rows.Append(',').Append(kind);
        rows.Append(',').Append(grounded ? '1' : '0');
        rows.Append(',');
        AppendCsvFloat(c != null ? Mathf.Repeat(c.debugFlapPhase, 1f) : 0f);
        rows.Append(',').Append(c != null && c.debugDownstroke ? '1' : '0');
        rows.Append(',');
        AppendCsvFloat(p.flight != null ? p.flight.lastForce.x : 0f);
        rows.Append(',').Append(p.snapped);
        rows.Append(',');
        AppendCsvInt(p.hasPrev ? p.prevMode : mode);
        rows.Append(',');
        AppendCsvFloat(NearestNeighbour(p.member, pos));
        rows.Append(',');
        AppendCsvFloat(residual);
        rows.Append(',');
        AppendCsvFloat(p.onsetTime >= 0f ? time - p.onsetTime : -1f);
        rows.Append(',').Append(eligible);
        // B-TAKEOFF-2: the leap's own share of vy and the wing plane's span.
        // Appended at the end so every column the earlier rounds compared
        // keeps its index.
        rows.Append(',');
        AppendCsvFloat(c != null ? c.debugLeapVy : 0f);
        rows.Append(',');
        AppendCsvFloat(WingSpan(p));
        // B-TAKEOFF-2: how much of this frame the wing was gliding (the pocket
        // blend) and whether the brake's own row was running.
        rows.Append(',');
        AppendCsvFloat(c != null ? c.debugGlideBlend : 0f);
        rows.Append(',');
        rows.Append(c != null && c.debugBrakeOn ? 1 : 0);
        // B-WING-3: the wing's deployment as the picture shows it — the tip's
        // reach from the body centre and the elbow/wrist bends — plus the
        // picture's own parachute window. Appended at the end, as before.
        rows.Append(',');
        AppendCsvFloat(WingReach(p));
        rows.Append(',');
        AppendCsvFloat(WingBend(p, 0));
        rows.Append(',');
        AppendCsvFloat(WingBend(p, 1));
        rows.Append(',').Append(c != null && c.debugChuteOn ? 1 : 0);
        // B-WING-4: the fan's own area — the drive as it was applied and the
        // chord it opens (the max perpendicular extension of the feather tips
        // off their own bone, metres). Appended at the end, as before.
        rows.Append(',');
        AppendCsvFloat(p.leftFan != null ? p.leftFan.Open01 : 0f);
        rows.Append(',');
        AppendCsvFloat(p.leftFan != null ? p.leftFan.Chord() : 0f);
        // B-GROUND-1: the claws. A ragdoll bird has no pads: zeros there.
        rows.Append(',');
        AppendCsvFloat(c != null ? c.debugClawLX : 0f);
        rows.Append(',');
        AppendCsvFloat(c != null ? c.debugClawLY : 0f);
        rows.Append(',');
        AppendCsvFloat(c != null ? c.debugClawRX : 0f);
        rows.Append(',');
        AppendCsvFloat(c != null ? c.debugClawRY : 0f);
        rows.Append(',').Append(c != null ? c.debugClawLOn : 0);
        rows.Append(',').Append(c != null ? c.debugClawROn : 0);
        rows.Append(',');
        AppendCsvFloat(c != null ? c.debugClawSlipL : 0f);
        rows.Append(',');
        AppendCsvFloat(c != null ? c.debugClawSlipR : 0f);
        rows.Append(',');
        AppendCsvFloat(c != null ? c.debugClawStride : 0f);
        // B-VIS-WALK: the drawn step (override) - phase and flag.
        rows.Append(',');
        AppendCsvFloat(c != null ? c.debugStepPhase : -1f);
        rows.Append(',').Append(c != null ? c.debugStepOn : 0);
        rows.AppendLine();

        p.frames++;
        if (p.onsetTime < 0f)
            p.framesPreOnset++;
        if (eligible == 1)
        {
            p.framesEligible++;
            p.sumSpeedEligible += Mathf.Abs(vel.x);
            if (Mathf.Abs(vel.x) >= MoveSpeedMps) p.framesMoving++;
        }
        else if (p.onsetTime >= 0f && mode == (int)BirdMode.Sit)
            p.framesHeldSit++;
    }

    // The onset rule: the clock starts at the member's own first takeoff or first own
    // locomotion, never at spawn; the spawn settle is excluded. A parked bird (mode
    // Sit, no motion) is a held frame: it fills no motion bar.
    private static bool HeldSit(int mode, Vector2 vel)
    {
        return (mode == (int)BirdMode.Sit || mode == (int)BirdMode.Stand)
            && Mathf.Abs(vel.x) < StepMoveSpeedMps
            && Mathf.Abs(vel.y) < StepMoveSpeedMps;
    }

    private void DetectPeerEvents(float time, Peer p, string kind, float height)
    {
        BirdSensors s = p.sensors;
        BirdController c = p.control;
        int mode = c != null ? (int)c.mode : 0;
        bool grounded = s.leftFootGrounded || s.rightFootGrounded;
        Vector2 vel = p.bodyRb != null ? p.bodyRb.linearVelocity : Vector2.zero;
        float pitch = s.bodyPitch;
        float speed = Mathf.Abs(vel.x);
        int facing = c != null && c.FacingSign() < 0f ? -1 : 1;
        // B-LAND-1: the flare is a NOSE-UP angle, and this rig bakes the
        // heading into the body rotation (BirdFlight.PitchGoal returns
        // goal * face), so a bird flying toward −X pitches negative. The
        // signed world pitch read every flare flown toward −X as 0 and the
        // flare_peak event could never fire on that half of the landings
        // whatever the base did. noseUp = pitch × facing is the angle the
        // film shows and the only honest flare reading.
        float noseUp = pitch * facing;

        if (p.onsetTime < 0f && time >= OnsetSettleSeconds)
        {
            bool takeoff = (mode == (int)BirdMode.Fly || mode == (int)BirdMode.Glide
                || mode == (int)BirdMode.Attack) && !grounded;
            bool walk = mode == (int)BirdMode.Walk && speed >= StepMoveSpeedMps;
            if (takeoff || walk || speed >= MoveSpeedMps)
            {
                p.onsetTime = time;
                p.onsetCause = takeoff ? "takeoff" : walk ? "walk" : "locomotion";
                AppendEvent(time, p, "onset", vel, pitch, 0f, kind, height,
                    p.airborneSince >= 0f ? time - p.airborneSince : -1f);
            }
        }

        if (p.hasPrev)
        {
            // B-TAKEOFF-2: the leap's own event. The base counts the impulse;
            // the ruler wants the moment and the vertical speed it carried.
            if (c != null && c.debugLeapAt > 0f && c.debugLeapAt > p.leapSeenAt)
            {
                p.leapSeenAt = c.debugLeapAt;
                AppendEvent(time, p, "leap", vel, c.debugLeapVy, c.debugLeapVy, kind, height, 0f);
            }
            if (p.prevGrounded && !grounded)
            {
                p.airborneSince = time;
                bool flap = mode == (int)BirdMode.Fly || mode == (int)BirdMode.Glide
                    || mode == (int)BirdMode.Attack;
                if (flap)
                {
                    p.takeoffs++;
                    p.lastTakeoff = time;
                    AppendEvent(time, p, "takeoff", vel, pitch, 0f, kind, height, 0f);
                }
                else
                {
                    // Honest signal the order asks the film to show: it left the ground
                    // with no flight mode — a drop, not a takeoff.
                    p.airDrops++;
                    AppendEvent(time, p, "airborne_no_flap", vel, pitch, height, kind, height, 0f);
                }
            }
            else if (!p.prevGrounded && grounded)
            {
                float airtime = p.airborneSince >= 0f ? time - p.airborneSince : -1f;
                p.touchdowns++;
                AppendEvent(time, p, "touchdown", vel, pitch, airtime, kind, height, airtime);
                if (airtime >= 0f && airtime < BounceAirtimeSeconds)
                {
                    p.bounces++;
                    AppendEvent(time, p, "bounce", vel, pitch, airtime, kind, height, airtime);
                }
                if (p.flarePeak >= FlareMinDegrees
                    && p.flarePeakTime >= 0f
                    && time - p.flarePeakTime <= FlareWindowSeconds)
                {
                    AppendEvent(time, p, "flare_peak", vel, p.flarePeak,
                        time - p.flarePeakTime, kind, height, airtime);
                }
                p.flarePeak = 0f;
                p.flarePeakTime = -1f;
                p.airborneSince = -1f;
            }

            if (p.prevMode != int.MinValue && mode != p.prevMode
                && mode == (int)BirdMode.Peck)
            {
                p.pecks++;
                AppendEvent(time, p, "peck", vel, pitch, 0f, kind, height,
                    p.airborneSince >= 0f ? time - p.airborneSince : -1f);
            }

            if (grounded && kind != p.prevSupport
                && (kind == "pad" || kind == "branch"))
            {
                p.perches++;
                AppendEvent(time, p, "perch", vel, pitch, 0f, kind, height, 0f);
            }

            if (p.voice != null && p.voice.CryCount > p.prevCry)
            {
                p.cries++;
                AppendEvent(time, p, "cry", vel, pitch, p.voice.CryCount, kind, height,
                    p.airborneSince >= 0f ? time - p.airborneSince : -1f);
            }

            if (facing != p.prevFacing && speed >= MoveSpeedMps)
            {
                if (p.turnStart >= 0f)
                    AppendEvent(time, p, "turn_end", vel, pitch, time - p.turnStart, kind, height, 0f);
                p.turnStart = time;
                p.turnCheck = time + 0.15f;
                p.turns++;
                AppendEvent(time, p, "turn_start", vel, pitch, 0f, kind, height, 0f);
            }
            else if (p.turnStart >= 0f && time >= p.turnCheck
                && facing == p.prevFacing && speed < TurnSettleSpeedMps)
            {
                AppendEvent(time, p, "turn_end", vel, pitch, time - p.turnStart, kind, height, 0f);
                p.turnStart = -1f;
            }
        }

        if (!grounded && noseUp > p.flarePeak)
        {
            p.flarePeak = noseUp;
            p.flarePeakTime = time;
        }
    }

    private void AdvancePeer(float time, Peer p)
    {
        BirdSensors s = p.sensors;
        p.prevPos = p.bodyRb != null ? p.bodyRb.position : (Vector2)p.body.position;
        p.prevVel = p.bodyRb != null ? p.bodyRb.linearVelocity : Vector2.zero;
        p.prevPitch = s.bodyPitch;
        p.prevGrounded = s.leftFootGrounded || s.rightFootGrounded;
        p.prevMode = p.control != null ? (int)p.control.mode : 0;
        p.prevFacing = p.control != null && p.control.FacingSign() < 0f ? -1 : 1;
        p.prevSupport = SupportOf(p, out _);
        p.prevCry = p.voice != null ? p.voice.CryCount : 0;
        p.hasPrev = true;
    }

    private void AppendEvent(float time, Peer p, string name, Vector2 vel, float pitch,
        float extra, string kind, float height, float airborneSeconds)
    {
        AppendFloatTo(events, time);
        events.Append(',');
        AppendIntTo(events, p.member);
        events.Append(',').Append(name).Append(',');
        AppendFloatTo(events, vel.x);
        events.Append(',');
        AppendFloatTo(events, vel.y);
        events.Append(',');
        AppendFloatTo(events, pitch);
        events.Append(',');
        AppendFloatTo(events, extra);
        events.Append(',');
        AppendIntTo(events, p.control != null ? (int)p.control.mode : 0);
        events.Append(',').Append(PeerLife(p).ToString());
        events.Append(',').Append(kind).Append(',');
        AppendFloatTo(events, airborneSeconds);
        events.Append(',');
        AppendFloatTo(events, height);
        events.AppendLine();
        eventCount++;
    }

    // The ruler's numbers, on their own clock: per-member onset, the excluded frames
    // and the post-onset motion bars. Written beside the rows, never into them.
    public string BuildOnsetJson()
    {
        StringBuilder json = new StringBuilder();
        json.Append('{');
        AppendStr(json, "subject", "bird", false);
        AppendStr(json, "label", info != null ? info.Label : "trial");
        AppendStr(json, "rule", "clock starts at the member's own first takeoff or own locomotion;"
            + " spawn settle excluded; parked sits fill no motion bar");
        AppendNum(json, "settleSeconds", OnsetSettleSeconds);
        AppendNum(json, "moveSpeedMps", MoveSpeedMps);
        AppendNum(json, "stepMoveSpeedMps", StepMoveSpeedMps);
        AppendNum(json, "bounceAirtimeSeconds", BounceAirtimeSeconds);
        AppendNum(json, "flareWindowSeconds", FlareWindowSeconds);
        AppendNum(json, "flareMinDegrees", FlareMinDegrees);
        AppendStr(json, "flareMeasure", "noseUpDegrees = bodyPitch * facing");
        AppendNum(json, "snapResidualMps", SnapResidualMps);
        json.Append(",\"members\":[");
        for (int i = 0; i < peerCount; i++)
        {
            Peer p = peers[i];
            if (p == null) continue;
            if (i > 0) json.Append(',');
            json.Append('{');
            AppendNum(json, "member", p.member, false);
            AppendStr(json, "birdKind", p.bird != null ? p.bird.kind.ToString() : "Crow");
            AppendStr(json, "onsetCause", p.onsetCause);
            AppendNum(json, "onsetTime", p.onsetTime);
            AppendNum(json, "frames", p.frames);
            AppendNum(json, "framesPreOnset", p.framesPreOnset);
            AppendNum(json, "framesEligible", p.framesEligible);
            AppendNum(json, "framesHeldSitExcluded", p.framesHeldSit);
            AppendNum(json, "framesMoving", p.framesMoving);
            AppendNum(json, "meanSpeedEligibleMps", p.framesEligible > 0
                ? (float)(p.sumSpeedEligible / p.framesEligible) : 0f);
            AppendNum(json, "moveShareEligible", p.framesEligible > 0
                ? (float)p.framesMoving / p.framesEligible : 0f);
            AppendNum(json, "snappedFrames", p.snappedFrames);
            json.Append(",\"events\":{");
            AppendNum(json, "onset", p.onsetTime >= 0f ? 1 : 0, false);
            AppendNum(json, "takeoff", p.takeoffs);
            AppendNum(json, "touchdown", p.touchdowns);
            AppendNum(json, "bounce", p.bounces);
            AppendNum(json, "peck", p.pecks);
            AppendNum(json, "perch", p.perches);
            AppendNum(json, "cry", p.cries);
            AppendNum(json, "turn", p.turns);
            AppendNum(json, "airborneNoFlap", p.airDrops);
            json.Append('}');
            json.Append('}');
        }
        json.Append(']');
        AppendNum(json, "eventRows", eventCount);
        json.Append('}');
        return json.ToString();
    }

    private void AppendFloatTo(StringBuilder sb, float value)
    {
        Span<char> buffer = numberBuffer;
        if (value.TryFormat(buffer, out int written, "G9", CultureInfo.InvariantCulture))
        {
            sb.Append(numberBuffer, 0, written);
            return;
        }

        sb.Append(value.ToString("G9", CultureInfo.InvariantCulture));
    }

    private void AppendIntTo(StringBuilder sb, int value)
    {
        Span<char> buffer = numberBuffer;
        if (value.TryFormat(buffer, out int written, default, CultureInfo.InvariantCulture))
        {
            sb.Append(numberBuffer, 0, written);
            return;
        }

        sb.Append(value.ToString(CultureInfo.InvariantCulture));
    }
}
