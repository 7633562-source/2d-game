using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

// Tree stand metrics. Read-only, no forces.
//
// T1 (TREES plan, 18.09): every metric here is computed from Unity primitives
// — Rigidbody2D.rotation/mass/inertia, HingeJoint2D.jointAngle/limits,
// transform positions — and never from a PlantTree helper
// (GetMaxAbsJointAngle / GetTrunkLeanDeg / GetHingePinnedFraction /
// GetHoldStats). A metric the fix can rewrite is not a measurement.
//
// The JSON is split in two: the run block carries MEASUREMENTS only, and the
// `build` block carries every build constant (counts, recipe, springs, limits,
// bind pose, hold torques, rig hash). A constant inside a run block is a
// constant-suspect key: `tree-const-assert.py` flags any run-block key that is
// bit-identical across two runs with different wind.
//
// T1-sway (19.09): the oak's copy of the apple's honest windows (same keys,
// same definitions, so one script reads both trees). `hingePinnedFraction` is a
// MAXIMUM INSTANTANEOUS fraction (a joint 0.2 deg off a stop for one step
// counts), and `swayAmpM` followed whichever segment was highest at each step,
// so the measured tip could change identity and swallow the startup transient.
// Both keys stay exactly as they were; the added windows measure the same
// geometry so the numbers mean what they look like:
//   - one tip segment, chosen once at the first sample and followed (a stable
//     identity), over the whole run and over a SETTLED window (t >= 4 s);
//   - pinning as a TIME fraction per joint, plus the worst overshoot past a
//     stop (the stops are soft, so "past the limit" is a real measurement);
//   - a wind-off run (`-treeWindUntil <s>`): the sail stops mid-run and the
//     residual over the LAST SECOND of the run is the decay evidence.
// Window definitions are constants and live in `build:`, never in the run block.
public class PlantTrialRecorder
{
    private struct Sensor
    {
        public WoodSensor wood;
        public HingeJoint2D joint;
        public Rigidbody2D rb;
    }

    private readonly PlantTree tree;
    private readonly TrialRunInfo info;
    private readonly TreeJointTrace trace;
    private readonly List<WoodSensor> wood = new List<WoodSensor>(40);
    private readonly List<Sensor> sensors = new List<Sensor>(32);

    private int samples;
    private double sumAbsJoint;
    private double sumJointSq;
    private float maxAbsJoint;
    private float startCrownY;
    private float minCrownY = float.MaxValue;
    private float lastAbsJoint;
    private float lastCrownY;
    private float settleAbsJoint;
    private bool settled;
    // Sway-never-topple metrics, same four keys as AppleTrialRecorder so the
    // oak and the apple stand are read by one probe script.
    private float maxTrunkLeanDeg;
    private float maxPinnedFraction;
    private float maxBaseSlideM;
    private float startCrownTipX;
    private float swayAmpM;
    // treeseq (req-269): bole tip excursion, written only when the switch is on
    // so an off run keeps its exact run block.
    private float boleTipMinX;
    private float boleTipMaxX;
    private bool boleTipValid;
    private float stumpSpawnX;
    private readonly float[] orderMaxAngle = new float[4];

    // ---- T1-sway windows (19.09) -------------------------------------------
    // The oak's copy of the apple's honest windows (T1-sway, build 3f93bfc4):
    // the same keys and the same window definitions, so one script reads both
    // trees. `hingePinnedFraction` above stays a MAXIMUM INSTANTANEOUS fraction
    // (a joint 0.2 deg off a stop for one step counts) and `swayAmpM` stays the
    // whole-run excursion against a tip that is re-picked every step; the keys
    // below measure the same geometry against ONE tip segment followed for the
    // run and a SETTLED window, plus the wind-off decay.
    // Measurement-window definitions. Constants: they go into `build:`.
    private const float SettleWindowStartS = 4f;
    private const float ResidualWindowS = 1f;
    private const float PinEpsDeg = 0.2f;   // same eps as hingePinnedFraction
    private int tipIndex = -1;
    private float tipStartX;
    private bool settledTipStarted;
    private float settledTipStartX;
    private float swayFixedTipAmpM;
    private float swaySettledAmpM;
    private float swaySettledMinX = float.MaxValue;
    private float swaySettledMaxX = float.MinValue;
    private int[] pinnedSamples = System.Array.Empty<int>();
    private int pinnedJointCount;
    private float maxHingeOverrunDeg;
    private float windOnMaxAbsJoint;
    private float windOffMaxAbsJoint;
    private float residualAbsJoint;
    private float residualTrunkLeanDeg;
    private float residualTipDxM;
    private readonly float windOffS;
    private readonly float residualFromS;

    // ---- The owner's landing sheet (25.09) ---------------------------------
    // The ring's own rows. The bend is measured from the LOADED ring mark's
    // Transform — a Unity primitive, like every other sensor here — never from a
    // PlantTree helper a fix could rewrite. Both stay 0 on a run that carries no
    // landing load (the ring is an opt-in build), so no accepted oak row moves.
    private readonly bool loadOnLanding;
    private readonly int landingSlot;
    private Transform landingMark;
    private Vector2 landingRest;
    private bool landingRestTaken;
    private float landingLoadDropM;
    private float landingLoadMaxDevM;
    private float landingLoadLastDevM;

    // ---- LND2 (26.09): the ring's own open-air read -------------------------
    // The plant's geometric read of the claw band (PlantTree.MeasureLandingSky),
    // taken once at the first sample while the physics world is live. Added
    // keys only: every one of them is 0 on a run with no ring, so no accepted
    // oak row moves.
    private bool landingSkyMeasured;
    private int landingSkyOpenCount;
    private float landingSkyBestM;
    private int landingSkyBestSlot = -1;
    private float landingSkyLoadSlotM;

    public PlantTrialRecorder(PlantTree tree, TrialRunInfo info)
    {
        this.tree = tree;
        this.info = info;
        if (tree != null)
        {
            tree.GetWoodSensors(wood);
            for (int i = 0; i < wood.Count; i++)
            {
                HumanSegment seg = wood[i].segment;
                if (seg == null) continue;
                if (wood[i].order < 0)
                {
                    stumpSpawnX = seg.transform.position.x;
                    continue;
                }
                sensors.Add(new Sensor { wood = wood[i], joint = seg.joint, rb = seg.rb });
            }
            startCrownY = CrownY();
            startCrownTipX = CrownTip().x;
            // The wind-off schedule and the pin counters are rig/run facts read
            // once, before the first step.
            windOffS = tree.windOffSeconds;
            pinnedSamples = new int[sensors.Count];
            // The landing ring's loaded mark, bound once (it is a child of the
            // wood, so its Transform already carries the bend).
            loadOnLanding = tree.loadOnLandingTrigger;
            landingSlot = tree.landingLoadSlot;
            Transform[] marks = tree.GetLandingMarkers();
            if (loadOnLanding && marks != null && marks.Length > 0)
                landingMark = marks[Mathf.Clamp(landingSlot, 0, marks.Length - 1)];
        }
        residualFromS = info != null && info.Duration > ResidualWindowS
            ? info.Duration - ResidualWindowS
            : -1f;
        trace = new TreeJointTrace(tree, Time.fixedDeltaTime);
    }

    public int JointColumnCount => trace.JointColumnCount;

    public string BuildJointCsv()
    {
        return trace.BuildCsv();
    }

    public void Sample(float time)
    {
        if (tree == null) return;
        float abs = MaxAbsJointAngle();
        float crown = CrownY();
        if (samples == 0)
            startCrownY = crown;
        lastAbsJoint = abs;
        lastCrownY = crown;
        if (abs > maxAbsJoint)
            maxAbsJoint = abs;
        if (crown < minCrownY)
            minCrownY = crown;
        sumAbsJoint += abs;
        sumJointSq += abs * abs;
        // Sway-never-topple: stem lean, hinge pinning, base slide and the
        // crown tip's horizontal excursion over the whole run.
        float trunkLean = TrunkLeanDeg();
        if (trunkLean > maxTrunkLeanDeg) maxTrunkLeanDeg = trunkLean;
        float pinned = HingePinnedFraction();
        if (pinned > maxPinnedFraction) maxPinnedFraction = pinned;
        float baseSlide = BaseSlideM();
        if (baseSlide > maxBaseSlideM) maxBaseSlideM = baseSlide;
        Vector2 crownTip = CrownTip();
        if (samples == 0) startCrownTipX = crownTip.x;
        float amp = Mathf.Abs(crownTip.x - startCrownTipX);
        if (amp > swayAmpM) swayAmpM = amp;
        if (tree.treeSeg > 0 && tree.BoleTip != null)
        {
            float bx = tree.BoleTip.transform.position.x;
            if (!boleTipValid)
            {
                boleTipMinX = bx;
                boleTipMaxX = bx;
                boleTipValid = true;
            }
            else
            {
                if (bx < boleTipMinX) boleTipMinX = bx;
                if (bx > boleTipMaxX) boleTipMaxX = bx;
            }
        }
        // T1-sway: the same excursion, but against ONE tip segment chosen once
        // (a stable identity) — whole run, then the settled window.
        if (samples == 0)
        {
            tipIndex = CrownTipIndex();
            tipStartX = TipX(tipIndex);
        }
        float fixedTipX = TipX(tipIndex);
        float fixedAmp = Mathf.Abs(fixedTipX - tipStartX);
        if (fixedAmp > swayFixedTipAmpM) swayFixedTipAmpM = fixedAmp;
        if (time >= SettleWindowStartS)
        {
            if (!settledTipStarted)
            {
                settledTipStarted = true;
                settledTipStartX = fixedTipX;
            }
            float settledAmp = Mathf.Abs(fixedTipX - settledTipStartX);
            if (settledAmp > swaySettledAmpM) swaySettledAmpM = settledAmp;
            if (fixedTipX < swaySettledMinX) swaySettledMinX = fixedTipX;
            if (fixedTipX > swaySettledMaxX) swaySettledMaxX = fixedTipX;
        }
        // Pinning as a TIME fraction per joint, plus the overshoot past the
        // stop itself (the stops are soft, so being past one is measurable).
        SampleHingePinning();
        // Wind phases: windOffS <= 0 means the sail never stops, so the whole
        // run is the wind-on phase and the wind-off phase stays empty.
        if (windOffS > 0f && time >= windOffS)
        {
            if (abs > windOffMaxAbsJoint) windOffMaxAbsJoint = abs;
        }
        else if (abs > windOnMaxAbsJoint)
        {
            windOnMaxAbsJoint = abs;
        }
        // Residual over the LAST second of the run: with the sail stopped at
        // windOffS this is the decay evidence; on a wind-on run it is the
        // steady state, labelled by windOffS in `build:`.
        if (residualFromS > 0f && time >= residualFromS)
        {
            if (abs > residualAbsJoint) residualAbsJoint = abs;
            if (trunkLean > residualTrunkLeanDeg) residualTrunkLeanDeg = trunkLean;
            if (fixedAmp > residualTipDxM) residualTipDxM = fixedAmp;
        }
        SampleLandingLoad();
        SampleLandingSky();
        SampleOrderMaxAngles(orderMaxAngle);
        // req-459 r1: per-joint sway share, only on the wood-switch cells.
        if (tree.treeWoodSagDeg > 0f || tree.treeMarginCeil > 0f) tree.SampleJointMaxAngles();
        if (tree.treeMarginCeil > 0f) tree.SampleSwayTrace(time);
        if (!settled && time >= 2f)
        {
            settleAbsJoint = abs;
            settled = true;
        }
        samples++;
        trace.Step(time);
    }

    // The bend the sheet is about: the loaded ring mark's own displacement from
    // the pose it was read at the first sample — so the rest pose is measured,
    // not assumed. `drop` is the downward part; `return` is the deviation left
    // at the last sample, which is the rebound of a released load.
    private void SampleLandingLoad()
    {
        if (landingMark == null)
            return;
        Vector2 p = landingMark.position;
        if (!landingRestTaken)
        {
            landingRest = p;
            landingRestTaken = true;
            return;
        }
        float drop = landingRest.y - p.y;
        if (drop > landingLoadDropM) landingLoadDropM = drop;
        float dev = (p - landingRest).magnitude;
        if (dev > landingLoadMaxDevM) landingLoadMaxDevM = dev;
        landingLoadLastDevM = dev;
    }

    // LND2 (26.09): the ring's own open-air read, taken ONCE at the first
    // sample — the pose the ring was built at, with the physics world live, so
    // the numbers are the plant's own geometry and not the bird lane's pixels.
    // The read itself lives in PlantTree (MeasureLandingSky); the rows below are
    // its summary: how many marks offer the open top edge, the best mark and its
    // point, and the loaded mark's own sky when the run carries a ring load.
    private void SampleLandingSky()
    {
        if (landingSkyMeasured)
            return;
        landingSkyMeasured = true;
        tree.MeasureLandingSky();
        int marks = tree.LandingSkyCount;
        for (int i = 0; i < marks; i++)
        {
            float sky = tree.GetLandingSky(i);
            if (sky >= PlantTree.LandingSkyMinM)
                landingSkyOpenCount++;
            if (sky > landingSkyBestM)
            {
                landingSkyBestM = sky;
                landingSkyBestSlot = i;
            }
            if (loadOnLanding && i == landingSlot)
                landingSkyLoadSlotM = sky;
        }
    }

    public string BuildSummaryJson(float simulatedSeconds, float wallSeconds = -1f)
    {
        StringBuilder json = new StringBuilder(512);
        json.Append("{");
        AppendNum(json, "simulatedSeconds", simulatedSeconds, false);
        if (wallSeconds >= 0f)
            AppendNum(json, "wallPerSim", simulatedSeconds > 0f ? wallSeconds / simulatedSeconds : 0f);
        json.Append(",\"samples\":").Append(samples.ToString(CultureInfo.InvariantCulture));
        AppendMetrics(json);
        if (info != null)
        {
            AppendNum(json, "duration", info.Duration);
            AppendStr(json, "sourceFingerprint", info.SourceFingerprint);
        }
        json.Append("}");
        return json.ToString();
    }

    // Run block: measurements only. Every build constant lives in `build`.
    public void AppendMetrics(StringBuilder json)
    {
        float mean = samples > 0 ? (float)(sumAbsJoint / samples) : 0f;
        float rms = samples > 0 ? (float)System.Math.Sqrt(sumJointSq / samples) : 0f;
        float drop = startCrownY - minCrownY;
        AppendNum(json, "maxAbsJointAngle", maxAbsJoint);
        AppendNum(json, "meanAbsJointAngle", mean);
        AppendNum(json, "rmsJointAngle", rms);
        AppendNum(json, "settleAbsJointAngle", settleAbsJoint);
        AppendNum(json, "finalAbsJointAngle", lastAbsJoint);
        AppendNum(json, "crownDrop", drop);
        AppendNum(json, "startCrownY", startCrownY);
        AppendNum(json, "minCrownY", minCrownY < float.MaxValue ? minCrownY : 0f);
        AppendNum(json, "finalCrownY", lastCrownY);
        AppendNum(json, "maxTrunkLeanDeg", maxTrunkLeanDeg);
        AppendNum(json, "hingePinnedFraction", maxPinnedFraction);
        AppendNum(json, "baseSlideM", maxBaseSlideM);
        AppendNum(json, "swayAmpM", swayAmpM);
        if (tree.treeSeg > 0)
        {
            // treeseq (req-269): the segmented bole's own numbers. Keys exist
            // only with the switch on, so an off run's JSON is unchanged.
            AppendNum(json, "trunkTipMinX", boleTipMinX);
            AppendNum(json, "trunkTipMaxX", boleTipMaxX);
            AppendNum(json, "trunkTipPkPkM", boleTipValid ? boleTipMaxX - boleTipMinX : 0f);
            AppendNum(json, "treeSegE", tree.TreeSegE);
            AppendNum(json, "treeSegMinEigK", tree.TreeSegMinMargin);
            AppendNum(json, "treeSegF1", tree.TreeSegF1);
            // req-358 r17: the explicit-integration margin and the crown floor dial.
            tree.RefreshStabPrint();
            AppendNum(json, "treeStabMargin", tree.TreeStabMargin);
            // req-440 r18: the wood margin's worst joint and the leaf block.
            AppendNum(json, "treeStabMarginLeaf", tree.TreeStabMarginLeaf);
            AppendNum(json, "treeStabMarginWorstJoint", tree.TreeStabMarginWorstJoint);
            AppendStr(json, "treeJ11Print", tree.TreeJ11Print ?? "");
            // req-440 r19: the leaf hinge load at the gust peak, m/s.
            AppendNum(json, "leafPeakRelSpeed", tree.TreeLeafPeakRel);
            // req-287 r13: the crown rule's two limb stiffnesses, N*m/rad.
            if (tree.treeCrownMargin > 0f)
            {
                AppendNum(json, "treeCrownK1", tree.TreeCrownK1);
                AppendNum(json, "treeCrownK2", tree.TreeCrownK2);
                AppendNum(json, "treeCrownFloor", tree.treeCrownFloor);
            }
        }
        if (tree.treeLeaves)
        {
            // treelvs (req-269 round 7): cluster count and the detach counter.
            // Keys exist only with the switch on, so an off run's JSON is unchanged.
            AppendNum(json, "leafClusterCount", tree.LeafClustersBuilt);
            AppendNum(json, "leafDetached", tree.LeavesDetached);
            // req-447 r21: one [leafload] line per cluster, log only.
            if (tree.treeLeafProbe > 0f) tree.PrintLeafProbe();
        }
        // req-459 r1: the tip-sway share table and the damping ratio per order, log only.
        if (tree.treeWoodSagDeg > 0f || tree.treeMarginCeil > 0f) tree.PrintSwayShare();
        // The trunk joint angle is read as a PAIR with its own limit: a lean
        // number without the stop it is measured against says nothing.
        AppendNum(json, "trunkJointAngleDeg", trace.TrunkJointAngleDeg);
        AppendNum(json, "trunkLimitDeg", trace.TrunkLimitDeg);
        // Per-step, per-joint trace metrics (TreeJointTrace, Unity primitives).
        AppendNum(json, "maxAbsAppliedTorqueNm", trace.MaxAbsAppliedTorqueNm);
        AppendNum(json, "springSatFraction", trace.SpringSatFraction);
        AppendNum(json, "holdResidualNm", trace.HoldResidualNm);
        AppendNum(json, "maxAbsSegmentWorldDevDeg", trace.MaxAbsSegmentWorldDevDeg);
        AppendNum(json, "crownComDxM", trace.CrownComDxM);
        for (int o = 0; o < orderMaxAngle.Length; o++)
            AppendNum(json, "treeOrder" + o + "MaxAngleDeg", orderMaxAngle[o]);
        // T1-sway windows (19.09). Added keys only — hingePinnedFraction and
        // swayAmpM above keep their old definitions for the record.
        AppendNum(json, "swayFixedTipAmpM", swayFixedTipAmpM);
        AppendNum(json, "swaySettledAmpM", swaySettledAmpM);
        AppendNum(json, "swaySettledPkPkM", swaySettledMaxX > swaySettledMinX
            ? swaySettledMaxX - swaySettledMinX : 0f);
        AppendNum(json, "worstJointPinnedTimeFraction", WorstJointPinnedTimeFraction());
        json.Append(",\"hingePinnedJointCount\":").Append(pinnedJointCount.ToString(CultureInfo.InvariantCulture));
        AppendNum(json, "maxHingeOverrunDeg", maxHingeOverrunDeg);
        AppendNum(json, "windOnMaxAbsJointAngle", windOnMaxAbsJoint);
        AppendNum(json, "windOffMaxAbsJointAngle", windOffMaxAbsJoint);
        AppendNum(json, "residualAbsJointAngleDeg", residualAbsJoint);
        AppendNum(json, "residualTrunkLeanDeg", residualTrunkLeanDeg);
        AppendNum(json, "residualTipDxM", residualTipDxM);
        // The owner's landing sheet (25.09). Added keys only: on a run with no
        // ring and no ring load they are 0, so no accepted oak row moves.
        AppendNum(json, "landingLoadDropM", landingLoadDropM);
        AppendNum(json, "landingLoadMaxDevM", landingLoadMaxDevM);
        AppendNum(json, "landingLoadReturnM", landingLoadLastDevM);
        // LND2 (26.09): the ring's own open-air read (added keys only; every one
        // of them 0 on a run with no ring, so no accepted oak row moves).
        json.Append(",\"landingSkyOpenCount\":")
            .Append(landingSkyOpenCount.ToString(CultureInfo.InvariantCulture));
        AppendNum(json, "landingSkyBestM", landingSkyBestM);
        json.Append(",\"landingSkyBestSlot\":")
            .Append(landingSkyBestSlot.ToString(CultureInfo.InvariantCulture));
        AppendNum(json, "landingSkyLoadSlotM", landingSkyLoadSlotM);
        TreeLandingProbe probe = tree.landingProbe;
        json.Append(",\"landingProbeSolidContacts\":")
            .Append((probe != null ? probe.SolidColliderHits : 0).ToString(CultureInfo.InvariantCulture));
        json.Append(",\"landingProbeSolidFrames\":")
            .Append((probe != null ? probe.SolidContactFrames : 0).ToString(CultureInfo.InvariantCulture));
        json.Append(",\"landingProbeTriggerHits\":")
            .Append((probe != null ? probe.TriggerHits : 0).ToString(CultureInfo.InvariantCulture));
        json.Append(",\"landingProbeTriggerFrames\":")
            .Append((probe != null ? probe.TriggerOverlapFrames : 0).ToString(CultureInfo.InvariantCulture));
        json.Append(",\"landingProbePathSteps\":")
            .Append((probe != null ? probe.PathSteps : 0).ToString(CultureInfo.InvariantCulture));
        AppendNum(json, "landingProbePathM", probe != null ? probe.PathLengthM : 0f);
        // The inventory the probe read back: how many solid tree colliders it had
        // to ignore for the bird, and how many stayed unguarded (must be 0).
        json.Append(",\"landingProbeGuardedSolid\":")
            .Append((probe != null ? probe.GuardedSolid : 0).ToString(CultureInfo.InvariantCulture));
        json.Append(",\"landingProbeUnguardedSolid\":")
            .Append((probe != null ? probe.UnguardedSolid : 0).ToString(CultureInfo.InvariantCulture));
        json.Append(",\"landingProbeTreeTriggers\":")
            .Append((probe != null ? probe.TreeTriggerColliders : 0).ToString(CultureInfo.InvariantCulture));
        // A solid contact is only evidence if it names itself and its step.
        AppendStr(json, "landingProbeFirstSolid", probe != null ? probe.FirstSolidName : string.Empty);
        json.Append(",\"landingProbeFirstSolidStep\":")
            .Append((probe != null ? probe.FirstSolidStep : 0).ToString(CultureInfo.InvariantCulture));
        json.Append(",\"landingProbeFirstSolidStayStep\":")
            .Append((probe != null ? probe.FirstSolidStayStep : 0).ToString(CultureInfo.InvariantCulture));
        AppendBuild(json);
    }

    // Build block: constants of the rig, read once and written into their own
    // object. Nothing here may move with the wind — if it does, it was not a
    // constant.
    private void AppendBuild(StringBuilder json)
    {
        if (tree == null) return;
        StringBuilder b = new StringBuilder(4096);
        if (trace.JointColumnCount > 0)
        {
            AppendStr(b, "buildHash", trace.BuildHash);
            AppendStr(b, "buildHashKind", trace.BuildHashKind);
            AppendNum(b, "fixedDelta", trace.FixedDelta);
            AppendNum(b, "jointColumnCount", trace.JointColumnCount);
            AppendNum(b, "bodyCount", trace.BodyCount);
            AppendNum(b, "kOverStabilityBound", trace.MaxStiffnessLimitBound);
        }
        AppendNum(b, "stumpSpawnX", stumpSpawnX);
        for (int o = 0; o < 4; o++)
        {
            AppendNum(b, "treeOrder" + o + "BindDeg", OrderBindDeg(o));
            AppendNum(b, "treeOrder" + o + "LimitDeg", OrderLimitDeg(o));
            AppendNum(b, "treeOrder" + o + "Stiffness", OrderStiffness(o));
            AppendNum(b, "treeOrder" + o + "GravityGradient", OrderGravityGradient(o));
        }
        b.Append(",\"treeSegmentCount\":").Append(tree.SegmentCount.ToString(CultureInfo.InvariantCulture));
        b.Append(",\"treeDynamicCount\":").Append(tree.DynamicBodyCount.ToString(CultureInfo.InvariantCulture));
        b.Append(",\"treeRigidbodyCount\":").Append(tree.RigidbodyCount.ToString(CultureInfo.InvariantCulture));
        b.Append(",\"treeWoodColliderCount\":").Append(tree.WoodColliderCount.ToString(CultureInfo.InvariantCulture));
        b.Append(",\"treePerchPadCount\":").Append(tree.PerchPadCount.ToString(CultureInfo.InvariantCulture));
        AppendStr(b, "treeRig", tree.rig.ToString());
        AppendStr(b, "treeKind", tree.ResolvedKind.ToString());
        AppendStr(b, "treeSeason", tree.ResolvedSeason.ToString());
        b.Append(",\"treeSeed\":").Append(tree.seed.ToString(CultureInfo.InvariantCulture));
        b.Append(",\"treeLeafCount\":").Append(tree.LeafCount.ToString(CultureInfo.InvariantCulture));
        b.Append(",\"treeBloomCount\":").Append(tree.BloomCount.ToString(CultureInfo.InvariantCulture));
        b.Append(",\"treeFruitCount\":").Append(tree.FruitCount.ToString(CultureInfo.InvariantCulture));
        // Lilac raceme / hazel catkin tally without AttachCluster cards.
        AppendNum(b, "treeLeafTintR", tree.LeafTintR);
        AppendNum(b, "treeLeafTintG", tree.LeafTintG);
        AppendNum(b, "treeLeafTintB", tree.LeafTintB);
        AppendNum(b, "treeLeafTintFarR", tree.LeafTintFarR);
        AppendNum(b, "treeLeafTintFarG", tree.LeafTintFarG);
        AppendNum(b, "treeLeafTintFarB", tree.LeafTintFarB);
        b.Append(",\"treeBarkPlateCount\":").Append(tree.BarkPlateCount.ToString(CultureInfo.InvariantCulture));
        b.Append(",\"treeCrownMassCount\":").Append(tree.CrownMassCount.ToString(CultureInfo.InvariantCulture));
        AppendNum(b, "treeCrownSizeX", tree.CrownSizeX);
        AppendNum(b, "treeCrownSizeY", tree.CrownSizeY);
        b.Append(",\"treeRootFlareCount\":").Append(tree.RootFlareCount.ToString(CultureInfo.InvariantCulture));
        b.Append(",\"treeForkCrotchCount\":").Append(tree.ForkCrotchCount.ToString(CultureInfo.InvariantCulture));
        b.Append(",\"treeLeafClusterCount\":").Append(tree.LeafClusterCount.ToString(CultureInfo.InvariantCulture));
        b.Append(",\"treeLeafSpriteCount\":").Append(tree.LeafSpriteCount.ToString(CultureInfo.InvariantCulture));
        b.Append(",\"treeLeafSwayCount\":").Append(tree.LeafSwayCount.ToString(CultureInfo.InvariantCulture));
        AppendNum(b, "treeLeafTilt", tree.LeafTiltDegrees);
        b.Append(",\"treeLeafPackTip\":").Append(tree.LeafPackTip ? "true" : "false");
        b.Append(",\"treeBranchSpurCount\":").Append(tree.BranchSpurCount.ToString(CultureInfo.InvariantCulture));
        b.Append(",\"treeBudCount\":").Append(tree.BudCount.ToString(CultureInfo.InvariantCulture));
        b.Append(",\"treeWinterWoodyCrown\":").Append(tree.WinterWoodyCrown ? "true" : "false");
        AppendNum(b, "treeWoodTwigR", tree.WoodTwigR);
        AppendNum(b, "treeWoodTwigG", tree.WoodTwigG);
        AppendNum(b, "treeWoodTwigB", tree.WoodTwigB);
        // Kind plates after season lerp. Winter orchard russet
        // must not stay a summer flake on a grey bole.
        AppendNum(b, "treeBarkLiteR", tree.BarkLiteR);
        AppendNum(b, "treeBarkLiteG", tree.BarkLiteG);
        AppendNum(b, "treeBarkLiteB", tree.BarkLiteB);
        b.Append(",\"treeGrowLeaves\":").Append(tree.growLeaves ? "true" : "false");
        // Orchard calendar. Spring/winter Apple fruitCount 0;
        // summer/autumn keep the crop. Oak stays false.
        b.Append(",\"treeGrowFruit\":").Append(tree.growFruit ? "true" : "false");
        b.Append(",\"treeHasLeafSway\":").Append(tree.HasLeafSway ? "true" : "false");
        b.Append(",\"treeHasLookSway\":").Append(tree.HasLookSway ? "true" : "false");
        AppendNum(b, "treeForkAngle", tree.forkAngle);
        // Build constant reported inside a run block until T1 — the
        // constant-suspect assertion named it, so it lives in `build:` now.
        AppendNum(b, "treeMaxAbsBranchHeading", tree.MaxAbsBranchHeading);
        AppendNum(b, "treeVisualTaper", tree.visualTaper);
        AppendNum(b, "treeWoodJoinScale", tree.WoodJoinScale);
        AppendNum(b, "treeWoodJoinInner", tree.WoodJoinScaleInner);
        AppendNum(b, "treeWoodTipLeader", tree.WoodTipScaleLeader);
        AppendNum(b, "treeWindSailTrunk", tree.WindSailTrunk);
        AppendNum(b, "treeWindSailTip", tree.WindSailTip);
        AppendNum(b, "treeWindSailSeason", tree.WindSailSeason);
        AppendNum(b, "treePictureSeasonSail", tree.PictureSeasonSail);
        AppendNum(b, "treeWindSailLean", tree.WindSailLean);
        // Oak tuft hub at 45°. PalmateTrack 0.70 flatten matches
        // palmate; 0.55 left leftover heading (flag on the stick).
        AppendNum(b, "treeLeafClusterRest", tree.LeafClusterRestDegrees);
        // Oak leader ~7.8° of fork 28. 8° deadzone left JSON at 0
        // (hat on the pole). SpreadLimbDeg 5 flattens PalmateTrack.
        AppendNum(b, "treeLeafClusterRestLeader", tree.LeafClusterRestLeader);
        AppendNum(b, "treeLeafClusterHangPad", tree.LeafClusterHangPad);
        AppendNum(b, "treeLeafLiveTrack", tree.LeafLiveTrack);
        AppendNum(b, "treeLeafCrownHangSail", tree.LeafCrownHangSail);
        AppendNum(b, "treeLeafClusterHangSail", tree.LeafClusterHangSail);
        b.Append(",\"treeLeafSwayWorldGust\":").Append(tree.LeafSwayWorldGust ? "true" : "false");
        AppendNum(b, "treeLeafSwayDegrees", tree.leafSwayDegrees);
        AppendNum(b, "treeWindSpeed", tree.windSpeed);
        AppendNum(b, "treeWindScale", tree.windScale);
        AppendNum(b, "treeWindGust", tree.windGust);
        AppendNum(b, "treeWindPeriod", tree.windPeriod);
        // Round 14: the field switch and its constants, so a field take names its
        // own gust model in the JSON instead of leaving it in the launcher line.
        AppendNum(b, "treeWindField", tree.treeWindField);
        AppendNum(b, "treeWindSigmaSlow", tree.treeWindSigmaSlow);
        AppendNum(b, "treeWindTauSlow", tree.treeWindTauSlow);
        AppendNum(b, "treeWindSigmaFast", tree.treeWindSigmaFast);
        AppendNum(b, "treeWindTauFast", tree.treeWindTauFast);
        AppendNum(b, "treeWindUAdv", tree.treeWindUAdv);
        AppendNum(b, "treeWindXUpwind", tree.treeWindXUpwind);
        AppendNum(b, "treeWindAlpha", tree.treeWindAlpha);
        AppendNum(b, "treeWindClamp", tree.treeWindClamp);
        AppendNum(b, "treeStabilityIndex", tree.MaxStabilityIndex);
        b.Append(",\"treeStabilityJoints\":").Append(tree.StabilityJointsCounted.ToString(CultureInfo.InvariantCulture));
        b.Append(",\"treeStabilitySkipped\":").Append(tree.StabilityJointsSkipped.ToString(CultureInfo.InvariantCulture));
        // Injected sitter load: a run that bends the crown must say which
        // load bent it, or the number is not evidence.
        AppendNum(b, "treePerchLoadKg", tree.perchLoadKg);
        b.Append(",\"treePerchLoadSlot\":").Append(tree.perchLoadSlot.ToString(CultureInfo.InvariantCulture));
        AppendNum(b, "treePerchLoadUntil", tree.perchLoadUntilSeconds);
        // The landing sheet's constants (25.09): the ring's size and kinds, the
        // slot a load targeted, and the probe's path definition.
        b.Append(",\"treeLandingTriggerCount\":")
            .Append(tree.LandingTriggerCount.ToString(CultureInfo.InvariantCulture));
        b.Append(",\"treeLandingTopCount\":")
            .Append(tree.LandingTopCount.ToString(CultureInfo.InvariantCulture));
        b.Append(",\"treeLandingForkCount\":")
            .Append(tree.LandingForkCount.ToString(CultureInfo.InvariantCulture));
        b.Append(",\"treeLandingSpanCount\":")
            .Append(tree.LandingSpanCount.ToString(CultureInfo.InvariantCulture));
        AppendNum(b, "treeLandingRadiusM", tree.LandingTriggerRadiusM);
        b.Append(",\"treeLoadOnLandingTrigger\":")
            .Append(tree.loadOnLandingTrigger ? "true" : "false");
        b.Append(",\"treeLandingLoadSlot\":")
            .Append(tree.landingLoadSlot.ToString(CultureInfo.InvariantCulture));
        b.Append(",\"treeProbeOn\":").Append(tree.landingProbe != null ? "true" : "false");
        AppendNum(b, "treeProbeBodyW", TreeLandingProbe.BirdBodyWidthM);
        AppendNum(b, "treeProbeBodyH", TreeLandingProbe.BirdBodyHeightM);
        // T1-sway: the wind-off schedule and the measurement-window definitions
        // are constants, so they live in `build:` and never wear a metric's name
        // in the run block.
        AppendNum(b, "windOffS", windOffS);
        AppendNum(b, "swaySettledWindowStartS", SettleWindowStartS);
        AppendNum(b, "residualWindowS", ResidualWindowS);
        b.Append(",\"tipSegmentIndex\":").Append(tipIndex.ToString(CultureInfo.InvariantCulture));
        // Hold torques are build-time feedforward, not a measurement: they were
        // one of the keys the constant-suspect assertion fired on.
        tree.GetHoldStats(out float meanHold, out float maxHold);
        AppendNum(b, "meanAbsHoldTorque", meanHold);
        AppendNum(b, "maxAbsHoldTorque", maxHold);
        trace.AppendJointBuildJson(b);

        string body = b.ToString();
        json.Append(",\"build\":{");
        json.Append(body.StartsWith(",") ? body.Substring(1) : body);
        json.Append('}');
    }

    // ---- sensor math: Unity primitives only ---------------------------------

    private float CrownY()
    {
        float y = tree.transform.position.y;
        for (int i = 0; i < wood.Count; i++)
        {
            HumanSegment seg = wood[i].segment;
            if (seg == null) continue;
            float top = seg.transform.position.y + seg.size.y * 0.5f;
            if (top > y) y = top;
        }
        return y;
    }

    private float MaxAbsJointAngle()
    {
        float max = 0f;
        for (int i = 0; i < sensors.Count; i++)
        {
            HingeJoint2D joint = sensors[i].joint;
            if (joint == null) continue;
            float abs = Mathf.Abs(Mathf.DeltaAngle(sensors[i].wood.bindDeg, joint.jointAngle));
            if (abs > max) max = abs;
        }
        return max;
    }

    private void SampleOrderMaxAngles(float[] maxAngleDeg)
    {
        if (maxAngleDeg == null) return;
        for (int i = 0; i < sensors.Count; i++)
        {
            HingeJoint2D joint = sensors[i].joint;
            if (joint == null) continue;
            int o = Mathf.Clamp(sensors[i].wood.order, 0, maxAngleDeg.Length - 1);
            float abs = Mathf.Abs(Mathf.DeltaAngle(sensors[i].wood.bindDeg, joint.jointAngle));
            if (abs > maxAngleDeg[o]) maxAngleDeg[o] = abs;
        }
    }

    private float TrunkLeanDeg()
    {
        for (int i = 0; i < sensors.Count; i++)
        {
            if (sensors[i].wood.order != 0) continue;
            HumanSegment seg = sensors[i].wood.segment;
            if (seg == null) continue;
            if (sensors[i].rb != null)
                return Mathf.Abs(Mathf.DeltaAngle(0f, sensors[i].rb.rotation));
            return Mathf.Abs(Mathf.DeltaAngle(0f, seg.transform.eulerAngles.z));
        }
        return 0f;
    }

    private float HingePinnedFraction()
    {
        int dynamic = 0, pinned = 0;
        const float epsDeg = 0.2f;
        for (int i = 0; i < sensors.Count; i++)
        {
            HingeJoint2D joint = sensors[i].joint;
            if (joint == null) continue;
            dynamic++;
            JointAngleLimits2D lim = joint.limits;
            float angle = joint.jointAngle;
            if (Mathf.Abs(angle - lim.min) <= epsDeg || Mathf.Abs(angle - lim.max) <= epsDeg)
                pinned++;
        }
        return dynamic > 0 ? (float)pinned / dynamic : 0f;
    }

    private float BaseSlideM()
    {
        for (int i = 0; i < wood.Count; i++)
        {
            if (wood[i].order >= 0) continue;
            HumanSegment seg = wood[i].segment;
            if (seg == null) continue;
            return Mathf.Abs(seg.transform.position.x - stumpSpawnX);
        }
        return 0f;
    }

    private Vector2 CrownTip()
    {
        Vector2 best = tree.transform.position;
        float bestY = float.MinValue;
        for (int i = 0; i < wood.Count; i++)
        {
            HumanSegment seg = wood[i].segment;
            if (seg == null) continue;
            float rad = seg.transform.eulerAngles.z * Mathf.Deg2Rad;
            Vector2 tip = (Vector2)seg.transform.position
                + new Vector2(-Mathf.Sin(rad), Mathf.Cos(rad)) * (seg.size.y * 0.5f);
            if (tip.y > bestY) { bestY = tip.y; best = tip; }
        }
        return best;
    }

    // ---- T1-sway sensor math (Unity primitives only) ----------------------

    // The tip SEGMENT, chosen once at the first sample by the same rule
    // CrownTip() uses (highest tip point). CrownTip() re-picks every step, so a
    // run can measure two different segments; this index cannot move.
    private int CrownTipIndex()
    {
        int best = -1;
        float bestY = float.MinValue;
        for (int i = 0; i < wood.Count; i++)
        {
            HumanSegment seg = wood[i].segment;
            if (seg == null) continue;
            float rad = seg.transform.eulerAngles.z * Mathf.Deg2Rad;
            float tipY = seg.transform.position.y + Mathf.Cos(rad) * (seg.size.y * 0.5f);
            if (tipY > bestY) { bestY = tipY; best = i; }
        }
        return best;
    }

    private float TipX(int index)
    {
        if (index < 0 || index >= wood.Count) return tree.transform.position.x;
        HumanSegment seg = wood[index].segment;
        if (seg == null) return tree.transform.position.x;
        float rad = seg.transform.eulerAngles.z * Mathf.Deg2Rad;
        return seg.transform.position.x - Mathf.Sin(rad) * (seg.size.y * 0.5f);
    }

    // Pinning per step, counted per joint: the same 0.2 deg band
    // hingePinnedFraction uses, but accumulated over samples so the report can
    // say "this hinge sat on its stop for X % of the run" instead of "at one
    // step, Y of 15 hinges were near a limit". The overrun is the distance PAST
    // the stop — the stops are soft, so it is usually nonzero.
    private void SampleHingePinning()
    {
        for (int i = 0; i < sensors.Count; i++)
        {
            HingeJoint2D joint = sensors[i].joint;
            if (joint == null) continue;
            JointAngleLimits2D lim = joint.limits;
            float angle = joint.jointAngle;
            float over = Mathf.Max(lim.min - angle, angle - lim.max);
            if (over > maxHingeOverrunDeg) maxHingeOverrunDeg = over;
            if (Mathf.Abs(angle - lim.min) <= PinEpsDeg || Mathf.Abs(angle - lim.max) <= PinEpsDeg)
            {
                if (i < pinnedSamples.Length)
                {
                    if (pinnedSamples[i] == 0) pinnedJointCount++;
                    pinnedSamples[i]++;
                }
            }
        }
    }

    private float WorstJointPinnedTimeFraction()
    {
        if (samples <= 0 || pinnedSamples.Length == 0) return 0f;
        int worst = 0;
        for (int i = 0; i < pinnedSamples.Length; i++)
            if (pinnedSamples[i] > worst) worst = pinnedSamples[i];
        return (float)worst / samples;
    }

    // ---- per-order build snapshot ------------------------------------------

    private float OrderBindDeg(int order)
    {
        float max = 0f;
        for (int i = 0; i < sensors.Count; i++)
        {
            if (Mathf.Clamp(sensors[i].wood.order, 0, 3) != order) continue;
            float abs = Mathf.Abs(sensors[i].wood.bindDeg);
            if (abs > max) max = abs;
        }
        return max;
    }

    private float OrderLimitDeg(int order)
    {
        float max = 0f;
        for (int i = 0; i < sensors.Count; i++)
        {
            if (Mathf.Clamp(sensors[i].wood.order, 0, 3) != order) continue;
            HingeJoint2D joint = sensors[i].joint;
            if (joint == null) continue;
            JointAngleLimits2D lim = joint.limits;
            float half = (lim.max - lim.min) * 0.5f;
            if (half > max) max = half;
        }
        return max;
    }

    private float OrderStiffness(int order)
    {
        float max = 0f;
        for (int i = 0; i < sensors.Count; i++)
        {
            if (Mathf.Clamp(sensors[i].wood.order, 0, 3) != order) continue;
            TreeSpring spring = sensors[i].wood.spring;
            if (spring == null) continue;
            if (spring.stiffness > max) max = spring.stiffness;
        }
        return max;
    }

    private float OrderGravityGradient(int order)
    {
        float max = 0f;
        for (int i = 0; i < sensors.Count; i++)
        {
            if (Mathf.Clamp(sensors[i].wood.order, 0, 3) != order) continue;
            TreeSpring spring = sensors[i].wood.spring;
            if (spring == null) continue;
            if (spring.gravityGradient > max) max = spring.gravityGradient;
        }
        return max;
    }

    private static void AppendNum(StringBuilder json, string key, float value, bool comma = true)
    {
        if (comma) json.Append(',');
        json.Append('"').Append(key).Append("\":");
        json.Append(value.ToString("0.########", CultureInfo.InvariantCulture));
    }

    private static void AppendStr(StringBuilder json, string key, string value)
    {
        json.Append(",\"").Append(key).Append("\":\"");
        if (!string.IsNullOrEmpty(value))
            json.Append(value.Replace("\\", "\\\\").Replace("\"", "\\\""));
        json.Append('"');
    }
}
