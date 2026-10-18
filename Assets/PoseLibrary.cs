using System.Globalization;
using UnityEngine;

// ─── level 2 pose library ───
// The walk's shapes as data. Joint conventions (Human.cs CreateLeg), degrees, with rig limits:
//   hip    minus = flexion, thigh forward                          -90 .. +45
//   knee   0 = straight, plus = flexion                              0 .. +120
//   ankle  heel - toe: plus = toe down / heel rise, minus = toe up  -45 .. +45
//   toe    MTP: minus = toe up (swing clearance), plus = toe down   -35 .. +50
//   trunk  pelvis / torso tilt targets are world tilts, minus = forward (GetPelvisTilt sign)
// Sample() is a pure function from metres and degrees to joint degrees (no torques or gains).
public static class PoseLibrary
{
    // One leg's pose. All four in degrees, joint conventions above.
    public struct LegPose
    {
        public float hip;
        public float knee;
        public float ankle;
        public float toe;
    }

    // One named pose: the swing leg, the stance (weight-bearing) leg and the pose's trunk targets.
    public struct Pose
    {
        public string name;
        public LegPose swing;
        public LegPose stance;
        public float pelvisTiltDeg;
        public float torsoTiltDeg;
    }

    // Everything the library is given: the BALANCE LAYER entries are that layer's decisions, the
    // rest are measurements or rig constants. Nothing here is a gain the library tunes.
    public struct Inputs
    {
        public float swingPhase;         // 0..1 of the swing: stepClock/swingDuration
        public float supportNeed;        // BALANCE LAYER, WALK CYCLE 6t: the CoM's own
                                         //   lead since the swap, as a share of
                                         //   comLeadSwapMeters: 0 = clearance owns the
                                         //   swing, 1 = the sole is asked for the floor
        public float landingShare;       // the swing share at which the foot is down
        public float footPlaceX;         // BALANCE LAYER: where the next foot lands, m
        public bool placeKneeLive;       // WALK CYCLE 75/76: the placement bias reads the
                                         //   knee the descent leaves (false = the
                                         //   drawn knee)
        public float swingAnkleDeg;      // WALK CYCLE 37: the swing ankle's own carry
                                         //   (deg, plus = toe down / heel rise), gated
                                         //   and ramped by the driver's tail gate; 0 = off
        public float hipHeightM;         // live pelvis height above the ground, m
        public float hipHeightTargetM;   // BALANCE LAYER: the height it holds, m
                                         //   - the step's datum, latched at the swap
                                         //   that started it (stand frame as ceiling),
                                         //   so hipHeightM minus it is the step's sink
        public float pelvisX;            // pelvis X (the pose is hip-relative), m
        public float pelvisTiltDeg;      // measured pelvis world tilt, minus = forward
        public float pelvisAngVelDeg;    // pelvis rate, same sign as the tilt, deg/s
        public float pelvisVelY;         // pelvis vertical rate, plus = rising, m/s
        public float thighM;             // segment lengths, m
        public float shinM;
        public float soleBelowAnkleM;    // ankle hinge height above the sole, m
                                         //   (Human.cs: ankle at the heel plate's
                                         //   centre, so the sole is half a foot below)
        public float swingLiftM;         // the SOLE clearance the swing foot is
                                         //   asked for, m (-poseLift)
        public float stanceFoldKneeDeg;  // WALK CYCLE 64: the drawn stance knee at the
        public float stancePlaceKneeDeg; //   SwingFold / FootPlace / PlantLoad keys
        public float stanceLoadKneeDeg;  //   (the ask itself; the drawn values default)
        public float heightGainDegPerM;  // the stand's verified height pair, as it is
        public float heightDampDegPerMps;
        public float hipMin, hipMax;     // Human.cs limits (pose clamp + the table's
        public float kneeMin, kneeMax;   // own audit), degrees
        public float ankleLimit;         // symmetric ankle pair, Human.cs:555
        public float toeMin, toeMax;
        public float stanceSoleHoldM;    // WALK CYCLE 28, from BALANCE LAYER: the sole
                                         //   lowering the recruit holds for the
                                         //   SUPPORT leg, metres (0 = not in force)
        public float stanceSoleLeverM;   // the sole's front-bottom corner about the ankle
                                         //   hinge, m - the arm the hold's ankle press
                                         //   is read against
    }

    // Every joint angle of the step, plus the trunk targets the same pose owns.
    // The witnesses (poseIndex .. trunkCorrDeg) are recorded, never read back.
    public struct Targets
    {
        public LegPose swingLeg;
        public LegPose stanceLeg;
        public float pelvisTiltTargetDeg;
        public float torsoTiltTargetDeg;
        public int poseIndex;
        public string poseName;
        public float nominalAnkleXM;
        public float clearanceM;
        public float placementErrorM;
        public float placementDeg;
        public float heightTrimDeg;
        public float trunkCorrDeg;
    }

    // ─── step-clock keys (landing key = Inputs.landingShare) ───
    public const float LaunchKey = 0.12f;
    public const float FoldKey = 0.35f;

    // ─── law constants ───
    // Swing fold band from the drawing ("swing leg must fold"): knee 60..80 deg while the foot is up.
    public const float FoldKneeMinDeg = 60f;
    public const float FoldKneeMaxDeg = 80f;
    // Descent knee ceiling, the drawn footplace knee: the descent brings the sole to the floor with
    // the leg under 20 deg before it lands, while the fold band above still owns the clearance.
    public const float PlaceKneeCeilDeg = 20f;
    // Trunk hold: hip deg per deg of pelvis tilt error and per deg/s of pelvis rate, the angle form
    // of pelvisPGain 2.0 / pelvisDGain 0.3 (first-pass values). Bounded to TrunkBandDeg so a tipped
    // body cannot rewrite the pose.
    public const float TrunkGainDegPerDeg = 0.6f;
    public const float TrunkDampDegPerDegPerSec = 0.02f;
    public const float TrunkBandDeg = 12f;
    // The hip-height request's own bound (the height pair itself - gain, damp - is the controller's
    // verified stand pair and is passed in, not retuned).
    public const float HeightTrimBandDeg = 12f;

    // Support sole hold: the clearance cap (m) up to which the hold can be fed, and the gate's load
    // bound (N). The joint delivers only a fraction of the ask, so StanceHoldDeliverP (below)
    // multiplies the support knee/ankle P demand while the hold is live; the D gains are untouched.
    public const float SupportHoldClearM = 0.0180f;
    public const float SupportHoldLoadN = 50f;

    // Tail gate's support-need bound (1 = the CoM lead the swap rule calls a swap); together with
    // SupportHoldLoadN it confines when the hold's latch may open.
    public const float PlantHoldNeedMin = 0.9f;
    // Stiff delivery gate: the support sole's clearance (m) must lie inside the ground flag's break
    // zone. Then: the hold band (deg), the hold ceiling (m), the per-frame release and the support
    // knee/ankle P multiplier applied while the hold is live.
    public const float SupportHoldStiffClearM = 0.0190f;
    public const float SupportHoldBandDeg = 24f;
    public const float SupportHoldCeilM = 0.050f;
    public const float SupportHoldReleasePerFrame = 0.94f;
    public const float StanceHoldDeliverP = 7f;

    // Afterfall crawl (DriveAfterfallCrawl): tucks are about half the body's own crawl, since full
    // deltas lift it off its palms and ArmDrive's presses never fire. Slow ramp and rock keep the
    // legs from punching; the crawl only keeps the body alive for the presses to find purchase.
    public const float AfterfallRampSeconds = 3f;
    public const float AfterfallRockSeconds = 6f;
    public const float AfterfallHipTuckDeg = 18f;
    public const float AfterfallKneeTuckDeg = 16f;
    public const float AfterfallAnkleTuckDeg = 12f;

    // ─── the walk poses ───
    // stance: the quiet stand pose the walk starts from (BalanceController hipBaseAngle -3 /
    //   kneeBaseAngle 6, the pose the verified quiet stance holds), both legs equal.
    public static readonly Pose Stance = Make("stance",
        Leg(-3f, 6f, 0f, 0f), Leg(-3f, 6f, 0f, 0f), 0f, 0f);

    // launch (the first push): the stance leg extends (-3 -> +6 hip) and rolls
    //   onto the toe (+14 ankle, +10 toe), the swing knee folds from 6 to 28 as
    //   the foot leaves, the toe lifts for clearance (toe -12).
    public static readonly Pose Launch = Make("launch",
        Leg(-16f, 28f, -6f, -12f), Leg(6f, 8f, 14f, 10f), -2f, -1f);

    // swing fold: the leg folded under the body - the drawing's band, knee 68
    //   (60..80), the thigh 30 deg forward of the pelvis, the toe up. The stance
    //   leg is nearly straight (knee 10) with the body passing over it.
    public static readonly Pose SwingFold = Make("swingfold",
        Leg(-30f, 68f, -10f, -14f), Leg(-6f, 10f, 4f, 2f), 0f, 0f);

    // foot place: the knee is back under 20 deg before the plant and the thigh
    //   carries the leg forward to the placement; the stance leg has begun to
    //   roll onto the toe for the push that follows.
    public static readonly Pose FootPlace = Make("footplace",
        Leg(-22f, 20f, -8f, -6f), Leg(2f, 12f, 8f, 6f), 0f, 0f);

    // plant / load: the just-landed leg takes the weight (knee softens to 24, absorbing the load)
    //   and the rear leg is at push-off. `swing` is the FRONT leg here: it landed at the previous
    //   key and the roles swap at the next step clock.
    public static readonly Pose PlantLoad = Make("plantload",
        Leg(-16f, 24f, -2f, 0f), Leg(10f, 10f, 16f, 12f), -1f, 0f);

    // Keyed in clock order: Stance 0.00, Launch 0.12, SwingFold 0.35, FootPlace
    // at the landing share, PlantLoad 1.00.
    public static readonly Pose[] WalkCycle = { Stance, Launch, SwingFold, FootPlace, PlantLoad };
    public static int PoseCount { get { return WalkCycle.Length; } }
    public static string PoseName(int index)
    {
        return index >= 0 && index < WalkCycle.Length ? WalkCycle[index].name : "";
    }

    private static LegPose Leg(float hip, float knee, float ankle, float toe)
    {
        LegPose l;
        l.hip = hip;
        l.knee = knee;
        l.ankle = ankle;
        l.toe = toe;
        return l;
    }

    private static Pose Make(string name, LegPose swing, LegPose stance,
                             float pelvisDeg, float torsoDeg)
    {
        Pose p;
        p.name = name;
        p.swing = swing;
        p.stance = stance;
        p.pelvisTiltDeg = pelvisDeg;
        p.torsoTiltDeg = torsoDeg;
        return p;
    }

    // ─── the clock: which pose, and how much of it ───
    public static Pose Blend(float phase, float landingShare)
    {
        return Blend(phase, landingShare,
                     SwingFold.stance.knee, FootPlace.stance.knee, PlantLoad.stance.knee);
    }

    // Same clock with the stance knee keys substituted at SwingFold / FootPlace / PlantLoad (genome
    // g[13..15] or the matching switches); at the drawn values it equals the overload above.
    public static Pose Blend(float phase, float landingShare,
                             float foldKneeDeg, float placeKneeDeg, float loadKneeDeg)
    {
        float land = Mathf.Clamp(landingShare, FoldKey + 0.05f, 0.98f);
        Pose fold = SwingFold;   fold.stance.knee = foldKneeDeg;
        Pose place = FootPlace;  place.stance.knee = placeKneeDeg;
        Pose load = PlantLoad;   load.stance.knee = loadKneeDeg;
        if (phase <= LaunchKey)
            return Mix(Stance, Launch, phase / LaunchKey);
        if (phase <= FoldKey)
            return Mix(Launch, fold, (phase - LaunchKey) / (FoldKey - LaunchKey));
        if (phase <= land)
            return Mix(fold, place, (phase - FoldKey) / (land - FoldKey));
        return Mix(place, load, Mathf.Clamp01((phase - land) / (1f - land)));
    }

    private static Pose Mix(Pose a, Pose b, float t)
    {
        t = Mathf.Clamp01(t);
        t = t * t * (3f - 2f * t);           // smoothstep: no step change at a key
        Pose p;
        p.name = t < 0.5f ? a.name : b.name;
        p.swing = MixLeg(a.swing, b.swing, t);
        p.stance = MixLeg(a.stance, b.stance, t);
        p.pelvisTiltDeg = Mathf.Lerp(a.pelvisTiltDeg, b.pelvisTiltDeg, t);
        p.torsoTiltDeg = Mathf.Lerp(a.torsoTiltDeg, b.torsoTiltDeg, t);
        return p;
    }

    private static LegPose MixLeg(LegPose a, LegPose b, float t)
    {
        LegPose l;
        l.hip = Mathf.Lerp(a.hip, b.hip, t);
        l.knee = Mathf.Lerp(a.knee, b.knee, t);
        l.ankle = Mathf.Lerp(a.ankle, b.ankle, t);
        l.toe = Mathf.Lerp(a.toe, b.toe, t);
        return l;
    }

    private static int NearestKey(float phase, float land)
    {
        // No array and no allocation: this runs once per physics step of a pose
        // walk, and the project's perf rule is no per-step garbage.
        int best = 0;
        float bestGap = Mathf.Abs(phase);
        float gap = Mathf.Abs(phase - LaunchKey);
        if (gap < bestGap) { bestGap = gap; best = 1; }
        gap = Mathf.Abs(phase - FoldKey);
        if (gap < bestGap) { bestGap = gap; best = 2; }
        gap = Mathf.Abs(phase - land);
        if (gap < bestGap) { bestGap = gap; best = 3; }
        gap = Mathf.Abs(phase - 1f);
        if (gap < bestGap) { best = 4; }
        return best;
    }

    // ─── the law ───
    public static Targets Sample(Inputs x)
    {
        Targets t = new Targets();
        Pose p = Blend(x.swingPhase, x.landingShare,
                       x.stanceFoldKneeDeg, x.stancePlaceKneeDeg, x.stanceLoadKneeDeg);
        float thigh = Mathf.Max(0.05f, x.thighM);
        float shin = Mathf.Max(0.05f, x.shinM);
        float land = Mathf.Clamp(x.landingShare, FoldKey + 0.05f, 0.98f);

        // Drawn pose, hip-relative. thighWorld / shinWorld are world angles from vertical, forward plus;
        // the hip joint angle is minus = flexion and the knee folds the shin back off the thigh, so
        // shinWorld = thighWorld - knee.
        float thighWorld = -p.swing.hip;
        float shinWorld = thighWorld - p.swing.knee;
        float thighRad = thighWorld * Mathf.Deg2Rad;
        float shinRad = shinWorld * Mathf.Deg2Rad;
        t.nominalAnkleXM = thigh * Mathf.Sin(thighRad) + shin * Mathf.Sin(shinRad);
        float drop = thigh * Mathf.Cos(thighRad) + shin * Mathf.Cos(shinRad);
        // Clearance the drawn swing pose gives: the SOLE's height above the ground at the live hip
        // height (the ankle hinge sits soleBelowAnkleM above the sole).
        t.clearanceM = x.hipHeightM - drop - x.soleBelowAnkleM;

        // 1. Sole height: two asks through one solve; the CoM's supportNeed decides which is in force.
        //    Clearance (-poseLift, m) may only RAISE the drawn fold, the knee capped to the 60..80 band.
        //    Descent (CoM-paced, not clock-paced) may only straighten the leg, never past PlaceKneeCeilDeg.
        float swingKnee = p.swing.knee;
        float clearanceAsk = x.swingLiftM * (1f - x.supportNeed);
        if (clearanceAsk > 0.001f)
        {
            float asked = KneeForDrop(
                x.hipHeightM - x.soleBelowAnkleM - clearanceAsk, thighWorld,
                thigh, shin, p.swing.knee);
            asked = Mathf.Clamp(asked, FoldKneeMinDeg, FoldKneeMaxDeg);
            // Past the fold key the hold weight is floored at 0.28, so the swing knee stays in the fold band
            // until supportNeed takes over the descent instead of expiring at the landing share. 0.28
            // carries the frame clock's knee at touch onto the step clock's arrival.
            float weight = Mathf.Max(FoldWeight(x.swingPhase, land),
                x.swingPhase > FoldKey ? 0.28f : 0f);
            if (asked > swingKnee)
                swingKnee += (asked - swingKnee) * weight;
        }
        else
        {
            float asked = KneeForDrop(
                x.hipHeightM - x.soleBelowAnkleM, thighWorld,
                thigh, shin, p.swing.knee);
            asked = Mathf.Clamp(asked, x.kneeMin, PlaceKneeCeilDeg);
            float weight = SmoothWindow(x.swingPhase, FoldKey, land) * x.supportNeed;
            if (asked < swingKnee)
                swingKnee += (asked - swingKnee) * weight;
        }

        // 2. Placement: the balance layer's foot position against the pose's own; 1 deg of hip moves the
        //    ankle by the pose's hip-to-ankle drop, so the gain is pose geometry. placeKneeLive uses the
        //    knee the descent leaves, since the drawn knee understates the reach (off by default).
        float nominalAnkleX = t.nominalAnkleXM;
        if (x.placeKneeLive)
        {
            float shinWorldLive = thighWorld - swingKnee;
            nominalAnkleX = thigh * Mathf.Sin(thighRad)
                + shin * Mathf.Sin(shinWorldLive * Mathf.Deg2Rad);
        }
        t.placementErrorM = x.footPlaceX - (x.pelvisX + nominalAnkleX);
        float placeWeight = SmoothWindow(x.swingPhase, FoldKey, land);
        float placeGain = Mathf.Rad2Deg / Mathf.Max(0.05f, drop);
        t.placementDeg = -placeGain * t.placementErrorM * placeWeight;

        // 3. Hip height: the verified stand loop's gain/damp pair, trimmed onto the knee and bounded.
        //    The stance leg holds the height outright; the swing leg gets the same trim as it comes
        //    down, so the foot meets the ground instead of hanging.
        float heightErrorM = x.hipHeightTargetM - x.hipHeightM;   // plus = the hip is low
        float trim = -(x.heightGainDegPerM * heightErrorM
                       + x.heightDampDegPerMps * x.pelvisVelY);
        t.heightTrimDeg = Mathf.Clamp(trim, -HeightTrimBandDeg, HeightTrimBandDeg);
        float swingHeightWeight = SmoothWindow(x.swingPhase, FoldKey, land);

        // 4. Trunk hold against the pose's pelvis target, with the verified pelvis law's signs: a
        //    positive hip angle is extensor work, and a pelvis forward of its target (error positive)
        //    is answered by extension.
        float pelvisErrorDeg = Mathf.DeltaAngle(x.pelvisTiltDeg, p.pelvisTiltDeg);
        t.trunkCorrDeg = Mathf.Clamp(
            TrunkGainDegPerDeg * pelvisErrorDeg
            - TrunkDampDegPerDegPerSec * x.pelvisAngVelDeg,
            -TrunkBandDeg, TrunkBandDeg);

        // The six joints. One owner: the library.
        t.swingLeg = p.swing;
        t.swingLeg.hip += t.placementDeg;
        t.swingLeg.knee = swingKnee + t.heightTrimDeg * swingHeightWeight;
        // Swing ankle: the drawn ankle plus the gated, ramped carry (0 = drawn ankle only);
        // ClampLeg below still holds the rig's stops.
        if (x.swingAnkleDeg != 0f)
            t.swingLeg.ankle += x.swingAnkleDeg;
        t.stanceLeg = p.stance;
        t.stanceLeg.hip += t.trunkCorrDeg;
        t.stanceLeg.knee += t.heightTrimDeg;
        // Support sole hold: a lowering of the support's sole, read about its own joints. The ankle
        // press moves the sole by the foot's front lever; the stance knee fold lifts the sole, so the
        // same lowering is asked of the knee about its lever. Both bounded by the band, then the stops.
        if (x.stanceSoleHoldM > 0f)
        {
            float ankleLeverM = Mathf.Max(0.02f, x.stanceSoleLeverM);
            t.stanceLeg.ankle += Mathf.Clamp(
                x.stanceSoleHoldM / ankleLeverM * Mathf.Rad2Deg, 0f, SupportHoldBandDeg);
            float stanceThighRad = -p.stance.hip * Mathf.Deg2Rad;
            float stanceKneeRad = p.stance.knee * Mathf.Deg2Rad;
            float kneeLeverM = Mathf.Max(0.02f,
                x.shinM * Mathf.Abs(Mathf.Sin(stanceThighRad - stanceKneeRad)));
            t.stanceLeg.knee -= Mathf.Clamp(
                x.stanceSoleHoldM / kneeLeverM * Mathf.Rad2Deg, 0f, SupportHoldBandDeg);
        }
        t.pelvisTiltTargetDeg = p.pelvisTiltDeg;
        t.torsoTiltTargetDeg = p.torsoTiltDeg;
        t.poseIndex = NearestKey(x.swingPhase, land);
        t.poseName = PoseName(t.poseIndex);
        t.swingLeg = ClampLeg(t.swingLeg, x);
        t.stanceLeg = ClampLeg(t.stanceLeg, x);
        return t;
    }

    // Knee flexion at which the hip-to-ankle drop is dropM with the thigh at thighWorldDeg, inverting
    //   drop = thigh*cos(tw) + shin*cos(tw - knee).
    // Cosine is even, so a drop has two knees (straight, folded); branchKneeDeg picks the branch.
    private static float KneeForDrop(float dropM, float thighWorldDeg, float thigh,
                                     float shin, float branchKneeDeg)
    {
        float tw = thighWorldDeg * Mathf.Deg2Rad;
        float c = Mathf.Clamp((dropM - thigh * Mathf.Cos(tw)) / Mathf.Max(0.01f, shin),
            -1f, 1f);
        float shinWorld = Mathf.Acos(c) * Mathf.Rad2Deg;
        float folded = thighWorldDeg + shinWorld;
        float straight = thighWorldDeg - shinWorld;
        return branchKneeDeg > (folded + straight) * 0.5f ? folded : straight;
    }

    // How much of the fold the clock is at: 0 at the launch key, 1 at the fold
    // key, back to 0 at the landing.
    private static float FoldWeight(float phase, float land)
    {
        if (phase <= FoldKey)
        {
            float u = Mathf.Clamp01((phase - LaunchKey) / (FoldKey - LaunchKey));
            return u * u * (3f - 2f * u);
        }
        float v = Mathf.Clamp01((phase - FoldKey) / Mathf.Max(0.05f, land - FoldKey));
        return 1f - v * v * (3f - 2f * v);
    }

    private static float SmoothWindow(float phase, float from, float to)
    {
        float u = Mathf.Clamp01((phase - from) / Mathf.Max(0.05f, to - from));
        return u * u * (3f - 2f * u);
    }

    // The library's own last word on a pose: every angle inside the rig's own
    // limits, so a pose is reachable by construction even before
    // ActuatorMixer.JointStable clamps the target to the live joint limits.
    private static LegPose ClampLeg(LegPose l, Inputs x)
    {
        l.hip = Mathf.Clamp(l.hip, x.hipMin, x.hipMax);
        l.knee = Mathf.Clamp(l.knee, x.kneeMin, x.kneeMax);
        l.ankle = Mathf.Clamp(l.ankle, -x.ankleLimit, x.ankleLimit);
        l.toe = Mathf.Clamp(l.toe, x.toeMin, x.toeMax);
        return l;
    }
}
