using System.Collections.Generic;
using System.Globalization;
using System.Text;

// WALK CYCLE 47 - THE PER-PLANT INSTRUMENT (Opus review #3, W1/R1). Recorder only.
//
// The walk lane has never printed its own plant series. lead@plant and
// chord@plant are in no table, and the pelvis height at the plant is measured
// nowhere in the tree; the fall window ([8.00, 9.50] at 20 ms steps) is the
// CONSEQUENCE of the plants and cannot tell a ratchet - each plant returns the
// body 1-2 cm lower, k4..k9, the last one out of margin - from a cliff (k4..k8
// return the same height and k9 alone loses 25 cm because the lead ran past
// reach). This class is the CAUSE series: one row per plant, written by the walk
// lane's own plant latch, and nothing else. No line of the round writes a joint,
// a force or a target.
//
// WHICH LATCH. The brief names captureStanceAnchor (BalanceControllerCaptureWalk
// .LatchCaptureStanceAnchor). Measured on the built row this round: tapTicks = 0
// and planValid = 0 on every frame of c6w47_walk, so that walker never ticks -
// the accepted command line (-poseDrive 1 -poseWalk 1) is walked by the level 2
// pose drive, and the capture latch produced 0 plants. The plant that exists on
// this row is the pose drive's own `posePlantFrame` (PoseDriveController.cs:1092:
// the frame the swing foot's sole comes back onto the floor after its own lift -
// "the same measured signal the step clock reads for its own swap"), the frame
// the lane's own brake distance capturePosePlaceLeadM is latched on. The
// instrument is attached there. The first pass's rows (attached to the dormant
// capture latch, one build, byte-inert) are reported, not kept.
//
// WHY A SECTION FILE AND NOT APPENDED COLUMNS. The lane keys on the row of
// record byte for byte (c6w38n_walk, md5 d76e2943; the stance a7e18eb9). Appended
// columns move every byte of that row, so the instrument lands in the run's own
// per-plant section instead - <label>_plants.csv, next to the summary JSON, the
// same pattern as the tree's _joints.csv. The brief allows it ("CSV columns
// and/or a per-plant JSON/CSV section - the worker's call"), and it is what keeps
// A1 (the byte identity of both rows of the pair) checkable at all.
//
// ONE ROW PER PLANT. A plant is the pose drive's own posePlantFrame: a contact
// that follows its own lift (a leg comes out of a swap already planted, so the
// leg that just landed does not count as planting again). k is the 1-based index
// of that event, counted from the walk's own first plant.
//
//   k           plant index, 1-based
//   time        Time.fixedTime at the plant, s
//   plantLeft   1 = the leg that planted is the LEFT leg, 0 = the right
//   stepIndex   the drive's own step counter at that frame (capturePoseStepIndex)
//   contactX    the planted plate's own measured x (the estimator's foot position
//               the step clock reads as swingFootX), world m
//   comX        comX of the same frame (state.comPosition.x), world m
//   lead        contactX - comX, m, signed: PLUS = the contact is AHEAD of the
//               CoM - the same convention as the row's own capturePosePlaceLeadM
//               ("capturePosePlaceXM minus the row's own comX ... + = the pivot
//               is AHEAD of the CoM"), which is the same event latched from the
//               ASK (placeX). More negative = further BEHIND the mass.
//   chord       Vector2.Distance(hip hinge, contact) at the plant, m - the
//               support chord, hip hinge (HipL/HipR anchor, the same point
//               BalanceController.CaptureHipPoint reads) to the planted plate. A
//               length, always positive.
//   hipY        the hip hinge's world y, m
//   pelvisY     the "Pelvis" object's world y, m (the same object the recorder's
//               own pelvis columns read)
//   planStepT   the plan's own swing time for the step in force, s (swingDuration;
//               swingDuration + stanceDuration is the full step period halfCycle)
//   plantClockT the swing clock's own elapsed time at the plant, s (stepClock, the
//               quantity capturePoseClockDone is compared against) - the ACTUAL T
//               of R2's second guard
//   landBlendU  the landing blend's own normalized progress at the plant:
//               clamp01((swingPhase - PoseLibrary.FoldKey) / (land - FoldKey)),
//               the same u the library's own SmoothWindow computes between the
//               fold key (0.35) and the landing key (land = the clamped landing
//               share, 0.80 by default). 1.000 = the plant arrived on the landing
//               key, u < 1 = the plant fired inside the blend.
//
// Every value is the live measurement of the frame the plant fired. Nothing in
// this file is written back to the body.
public static class WalkPlantLog
{
    public struct Row
    {
        public int k;
        public float time;
        public float plantLeft;
        public float stepIndex;
        public float contactX;
        public float comX;
        public float lead;
        public float chord;
        public float hipY;
        public float pelvisY;
        public float planStepT;
        public float plantClockT;
        public float landBlendU;
    }

    public const string Header =
        "k;time;plantLeft;stepIndex;contactX;comX;lead;chord;hipY;pelvisY;planStepT;"
        + "plantClockT;landBlendU";

    private static readonly List<Row> rows = new List<Row>(64);

    public static int Count => rows.Count;

    // A trial is one process; the log is cleared where the recorder is built.
    public static void Reset()
    {
        rows.Clear();
    }

    public static void Record(Row row)
    {
        rows.Add(row);
    }

    public static string BuildCsv()
    {
        StringBuilder sb = new StringBuilder(Header.Length + 64 + rows.Count * 128);
        sb.Append(Header).Append("\r\n");
        for (int i = 0; i < rows.Count; i++)
        {
            Row r = rows[i];
            sb.Append(r.k.ToString(CultureInfo.InvariantCulture)).Append(';')
              .Append(F(r.time)).Append(';')
              .Append(F(r.plantLeft)).Append(';')
              .Append(F(r.stepIndex)).Append(';')
              .Append(F(r.contactX)).Append(';')
              .Append(F(r.comX)).Append(';')
              .Append(F(r.lead)).Append(';')
              .Append(F(r.chord)).Append(';')
              .Append(F(r.hipY)).Append(';')
              .Append(F(r.pelvisY)).Append(';')
              .Append(F(r.planStepT)).Append(';')
              .Append(F(r.plantClockT)).Append(';')
              .Append(F(r.landBlendU)).Append("\r\n");
        }
        return sb.ToString();
    }

    // The recorder's own number format, so a reader of the lane's csv reads this
    // the same way (TrialRecorder.F, F4 invariant).
    private static string F(float value)
    {
        return value.ToString("F4", CultureInfo.InvariantCulture);
    }
}
