using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

// ─── LINE 2 — THE RESTITUTION RECORD (BallHail.cs, the owner's order 27.09.2026) ───
//
// One row per CONTACT. Every field the order names is on the row: the ball id, the
// contact point's segment, the hit position, the incoming velocity vector, the
// outgoing velocity vector, the incident angle and the MEASURED restitution
// e = |v_out . n| / |v_in . n| along the contact normal.
//
// The rows are flushed as they happen (one append per contact, AutoFlush): a take
// that ends early - a kill, a timeout - keeps every row that already happened.
// Two restitutions are recorded, because a moving segment is not a wall:
//   e_ball = |v_out . n| / |v_in . n|            (the order's own formula, ball only)
//   e_rel  = |(v_out - segV_out) . n| / |(v_in - segV_in) . n|   (relative - the
//            physically meaningful one when the segment answers back)
// Plus the engine's own reading (Collision2D.relativeVelocity) as the A/B witness,
// and both bodies' velocity vectors, so the audit can recompute anything.
//
// -1 in a numeric column means "not measured / not valid" (a grazing contact with
// |v_in . n| below 0.02 m/s has no honest restitution ratio).
public class BallImpactRecorder
{
    private const float BaselineWindowStart = 4.0f;   // same convention as TrialRecorder
    private const float BaselineWindowEnd = 5.0f;
    private const float MinNormalSpeed = 0.02f;       // below this no honest ratio exists

    private readonly Human human;
    private readonly BallHailShooter shooter;
    private readonly string label;
    private readonly string outputFolder;
    private readonly string sourceFingerprint;
    private readonly string buildGuid;
    private readonly float fixedDelta;
    private readonly float startY;

    private BalanceController balance;
    private VestibularSystem vestibular;
    private Transform pelvis;
    private bool resolved;

    private FallCriterion.Latch fallLatch;
    private FallCriterion.Baseline standBaseline;
    private float fallTime = -1f;
    private bool downNow;
    private int locoStateMax;
    private int locoStateEnd;
    private int balanceStateMax;
    private float minPelvisY = float.MaxValue;
    private float maxTiltDeg;
    private float lastTime;

    private readonly List<float> perBallMinDistance = new List<float>();
    private readonly List<int> perBallContacts = new List<int>();
    private int contactRows;

    private StreamWriter writer;
    private bool headerWritten;

    public BallImpactRecorder(Human human, BallHailShooter shooter, string label, string outputFolder,
        string sourceFingerprint, string buildGuid, float fixedDelta, float startY)
    {
        this.human = human;
        this.shooter = shooter;
        this.label = label;
        this.outputFolder = outputFolder;
        this.sourceFingerprint = sourceFingerprint;
        this.buildGuid = buildGuid;
        this.fixedDelta = fixedDelta;
        this.startY = startY;
    }

    public string CsvPath
    {
        get { return Path.Combine(outputFolder, label + "_ballhits.csv"); }
    }

    public string SummaryPath
    {
        get { return Path.Combine(outputFolder, label + "_ball.json"); }
    }

    // ─── ONE CONTACT ───
    public void OnContact(BallContactInfo info)
    {
        try
        {
            EnsureWriter();
            Vector2 n = info.normal;
            Vector2 vIn = info.velocityIn;
            Vector2 vOut = info.velocityOut;
            float inNormal = -Vector2.Dot(vIn, n);        // n faces the incoming motion
            float outNormal = Vector2.Dot(vOut, n);
            float outNormalAbs = Mathf.Abs(outNormal);
            float speedIn = vIn.magnitude;

            Vector2 relIn = vIn - info.segmentVelocityIn;
            Vector2 relOut = vOut - info.segmentVelocityOut;
            float relInNormal = -Vector2.Dot(relIn, n);
            float relOutNormal = Mathf.Abs(Vector2.Dot(relOut, n));

            float eBall = inNormal >= MinNormalSpeed ? outNormalAbs / inNormal : -1f;
            float eRel = relInNormal >= MinNormalSpeed ? relOutNormal / relInNormal : -1f;
            float incidentDeg = speedIn > 1e-4f
                ? Mathf.Acos(Mathf.Clamp(inNormal / speedIn, -1f, 1f)) * Mathf.Rad2Deg
                : -1f;

            StringBuilder row = new StringBuilder(320);
            row.Append(info.ballId.ToString(CultureInfo.InvariantCulture)).Append(';');
            row.Append(F(info.ballMass)).Append(';');
            row.Append(Escape(info.pointText)).Append(';');
            row.Append(Escape(info.angleName)).Append(';');
            row.Append(Escape(info.partAimed)).Append(';');
            row.Append(F(info.speedPlanned)).Append(';');
            row.Append(F(info.time)).Append(';');
            row.Append(Escape(info.segment)).Append(';');
            row.Append(info.isHuman ? "1" : "0").Append(';');
            row.Append(F(info.hitPoint.x)).Append(';');
            row.Append(F(info.hitPoint.y)).Append(';');
            row.Append(F(n.x)).Append(';');
            row.Append(F(n.y)).Append(';');
            row.Append(F(vIn.x)).Append(';');
            row.Append(F(vIn.y)).Append(';');
            row.Append(F(vOut.x)).Append(';');
            row.Append(F(vOut.y)).Append(';');
            row.Append(F(info.segmentVelocityIn.x)).Append(';');
            row.Append(F(info.segmentVelocityIn.y)).Append(';');
            row.Append(F(info.segmentVelocityOut.x)).Append(';');
            row.Append(F(info.segmentVelocityOut.y)).Append(';');
            row.Append(F(inNormal)).Append(';');
            row.Append(F(outNormalAbs)).Append(';');
            row.Append(F(incidentDeg)).Append(';');
            row.Append(F(eBall)).Append(';');
            row.Append(F(eRel)).Append(';');
            row.Append(F(info.engineRelativeSpeed)).Append(';');
            row.Append(F(info.ballBounciness)).Append(';');
            row.Append(F(info.segmentBounciness)).Append(';');
            row.Append(info.contactsSoFar.ToString(CultureInfo.InvariantCulture));
            row.Append(';').Append(F(info.aimShift));
            row.Append(';').Append(Escape(info.aimPreHit));
            row.Append(';').Append(info.aimOccluded ? "1" : "0");
            // b4: the reachability block - the shot's own muzzle distance and the aim
            // point's offset from the segment anchor (the disc search's footprint).
            row.Append(';').Append(F(info.standoff));
            row.Append(';').Append(F(info.aimDx));
            row.Append(';').Append(F(info.aimDy));

            writer.WriteLine(row.ToString());
            contactRows++;
        }
        catch (Exception e)
        {
            Debug.LogError("BallImpactRecorder: contact row failed: " + e.Message);
        }
    }

    private void EnsureWriter()
    {
        if (writer != null && headerWritten)
            return;
        Directory.CreateDirectory(outputFolder);
        writer = new StreamWriter(CsvPath, false, new UTF8Encoding(false));
        writer.AutoFlush = true;   // every contact is on disk the moment it happens
        writer.WriteLine("ball;ballMass;point;angle;part;speedPlanned;t;segment;human;hitX;hitY;nx;ny;"
            + "vInX;vInY;vOutX;vOutY;segVInX;segVInY;segVOutX;segVOutY;vInNormal;vOutNormal;"
            + "incidentDeg;e_ball;e_rel;engineRel;ballBounce;segBounce;contactsSoFar;"
            + "aimShift;aimPreHit;aimOccluded;standoff;aimDx;aimDy");
        headerWritten = true;
        Debug.Log("BALLHITS_CSV " + CsvPath);
    }

    private static string F(float value)
    {
        if (float.IsNaN(value) || float.IsInfinity(value))
            return "-1";
        return value.ToString("0.######", CultureInfo.InvariantCulture);
    }

    private static string Escape(string value)
    {
        return (value ?? string.Empty).Replace(';', ' ').Replace('\n', ' ').Trim();
    }

    // ─── ONE STEP: the human's own status (the acceptance's second half) ───
    public void Sample(float time)
    {
        lastTime = time;
        Resolve();
        float pelvisY = pelvis != null ? pelvis.position.y : 0f;
        float tilt = vestibular != null ? vestibular.GetBodyTilt() : 0f;
        minPelvisY = Mathf.Min(minPelvisY, pelvisY);
        maxTiltDeg = Mathf.Max(maxTiltDeg, Mathf.Abs(tilt));

        if (time >= BaselineWindowStart && time < BaselineWindowEnd)
            standBaseline.Feed(pelvisY, tilt);
        else if (time >= BaselineWindowEnd)
            standBaseline.Freeze();
        float baselineHeight = standBaseline.valid ? standBaseline.height : float.NegativeInfinity;
        downNow = FallCriterion.Step(ref fallLatch, baselineHeight, pelvisY, tilt, fixedDelta, time);
        if (downNow && fallTime < 0f && fallLatch.fired)
            fallTime = fallLatch.fireTime;

        if (balance != null)
        {
            int loco = (int)balance.locomotionState;
            int state = (int)balance.currentState;
            if (loco > locoStateMax) locoStateMax = loco;
            if (state > balanceStateMax) balanceStateMax = state;
            locoStateEnd = loco;
        }

        if (shooter != null)
        {
            IReadOnlyList<BallHailShooter.BallShotPlan> plan = shooter.Plan;
            while (perBallMinDistance.Count < plan.Count)
            {
                perBallMinDistance.Add(float.MaxValue);
                perBallContacts.Add(0);
            }
            for (int i = 0; i < plan.Count; i++)
            {
                BallHailProjectile projectile = shooter.ProjectileFor(plan[i].index);
                if (projectile == null)
                    continue;
                if (projectile.MinAimDistanceM < perBallMinDistance[i])
                    perBallMinDistance[i] = projectile.MinAimDistanceM;
                perBallContacts[i] = projectile.Contacts;
            }
        }
    }

    private void Resolve()
    {
        if (resolved)
            return;
        resolved = true;
        if (human == null)
            return;
        balance = human.GetComponent<BalanceController>();
        vestibular = human.GetComponent<VestibularSystem>();
        pelvis = human.transform.Find("Pelvis");
    }

    // ─── THE END OF THE TAKE ───
    public void WriteFiles(float simulated, float wallSeconds)
    {
        try
        {
            EnsureWriter();
            writer.Flush();
            writer.Dispose();
            writer = null;

            bool falling = locoStateMax >= 2;          // Falling(2) / Fallen(3) / GettingUp(4)
            bool standOk = !downNow && !falling;

            StringBuilder json = new StringBuilder(2048);
            json.Append("{\n");
            json.Append("  \"label\": ").Append(J(label)).Append(",\n");
            json.Append("  \"line\": \"BALL HAIL (line 2, Yury 27.09.2026)\",\n");
            json.Append("  \"sourceFingerprint\": ").Append(J(sourceFingerprint)).Append(",\n");
            json.Append("  \"buildGuid\": ").Append(J(buildGuid)).Append(",\n");
            json.Append("  \"simulatedSeconds\": ").Append(F(simulated)).Append(",\n");
            json.Append("  \"wallSeconds\": ").Append(F(wallSeconds)).Append(",\n");
            json.Append("  \"rowsCsv\": ").Append(J(CsvPath)).Append(",\n");
            json.Append("  \"contactRows\": ").Append(contactRows.ToString(CultureInfo.InvariantCulture)).Append(",\n");

            json.Append("  \"ball\": {");
            json.Append("\"radiusM\": ").Append(F(shooter != null ? shooter.radiusMeters : 0f));
            json.Append(", \"massKg\": ").Append(F(shooter != null ? shooter.massKg : 0f));
            json.Append(", \"bounciness\": ").Append(F(shooter != null ? shooter.bounciness : -1f));
            json.Append(", \"friction\": ").Append(F(shooter != null ? shooter.friction : -1f));
            json.Append(", \"gravityScale\": ").Append(F(shooter != null ? shooter.gravityScale : 0f));
            json.Append(", \"lifetimeS\": ").Append(F(shooter != null ? shooter.lifetimeSeconds : 0f));
            json.Append(", \"spawnDistanceM\": ").Append(F(shooter != null ? shooter.spawnDistance : 0f));
            json.Append("},\n");

            json.Append("  \"segmentMaterials\": {\"applied\": ").Append(shooter != null && shooter.SegmentMaterialsApplied ? "true" : "false");
            json.Append(", \"bounciness\": ").Append(F(shooter != null ? shooter.segmentBounciness : -1f));
            json.Append(", \"friction\": ").Append(F(shooter != null ? shooter.segmentFriction : -1f));
            json.Append(", \"note\": \"feet/toes/hands keep the rig's own grip materials\"},\n");

            json.Append("  \"aimFix\": {\"enabled\": ").Append(shooter != null && shooter.aimFix ? "true" : "false");
            json.Append(", \"rangeM\": ").Append(F(shooter != null ? shooter.aimFixRange : 0f));
            json.Append(", \"stepM\": ").Append(F(shooter != null ? shooter.aimFixStep : 0f));
            json.Append(", \"circleCast\": ").Append(shooter != null && shooter.aimCircleCast ? "true" : "false").Append("},\n");

            json.Append("  \"engine\": {");
            json.Append("\"bounceThreshold\": ").Append(F(Physics2D.bounceThreshold));
            json.Append(", \"gravityY\": ").Append(F(Physics2D.gravity.y));
            json.Append(", \"fixedDelta\": ").Append(F(fixedDelta));
            json.Append("},\n");

            json.Append("  \"human\": {");
            json.Append("\"fell\": ").Append(downNow ? "true" : "false");
            json.Append(", \"fallTime\": ").Append(F(fallTime));
            json.Append(", \"locoStateMax\": ").Append(locoStateMax.ToString(CultureInfo.InvariantCulture));
            json.Append(", \"locoStateEnd\": ").Append(locoStateEnd.ToString(CultureInfo.InvariantCulture));
            json.Append(", \"balanceStateMax\": ").Append(balanceStateMax.ToString(CultureInfo.InvariantCulture));
            json.Append(", \"minPelvisY\": ").Append(F(minPelvisY == float.MaxValue ? startY : minPelvisY));
            json.Append(", \"maxTiltDeg\": ").Append(F(maxTiltDeg));
            json.Append(", \"stayedStanding\": ").Append(standOk ? "true" : "false");
            json.Append("},\n");

            json.Append("  \"shots\": [");
            if (shooter != null)
            {
                IReadOnlyList<BallHailShooter.BallShotPlan> plan = shooter.Plan;
                for (int i = 0; i < plan.Count; i++)
                {
                    if (i > 0) json.Append(',');
                    BallHailShooter.BallShotPlan shot = plan[i];
                    json.Append("\n    {\"index\": ").Append(shot.index.ToString(CultureInfo.InvariantCulture));
                    json.Append(", \"point\": ").Append(J(shot.pointText));
                    json.Append(", \"angle\": ").Append(J(shot.angleName));
                    json.Append(", \"part\": ").Append(J(shot.part));
                    json.Append(", \"speed\": ").Append(F(shot.speedMps));
                    json.Append(", \"at\": ").Append(F(shot.timeSeconds));
                    json.Append(", \"standoff\": ").Append(F(shot.distanceM));
                    json.Append(", \"fired\": ").Append(shot.fired ? "true" : "false");
                    json.Append(", \"aimShift\": ").Append(F(shot.aimShift));
                    json.Append(", \"aimDx\": ").Append(F(shot.aimDx));
                    json.Append(", \"aimDy\": ").Append(F(shot.aimDy));
                    json.Append(", \"aimPreHit\": ").Append(J(shot.aimPreHit));
                    json.Append(", \"aimOccluded\": ").Append(shot.aimOccluded ? "true" : "false");
                    json.Append(", \"contacts\": ").Append(i < perBallContacts.Count
                        ? perBallContacts[i].ToString(CultureInfo.InvariantCulture) : "0");
                    json.Append(", \"minAimDistanceM\": ").Append(i < perBallMinDistance.Count
                        ? F(perBallMinDistance[i] == float.MaxValue ? -1f : perBallMinDistance[i]) : "-1");
                    json.Append('}');
                }
            }
            json.Append("\n  ],\n");
            json.Append("  \"stayedStanding\": ").Append(standOk ? "true" : "false").Append('\n');
            json.Append("}\n");

            File.WriteAllText(SummaryPath, json.ToString());
            Debug.Log("BALL_SUMMARY " + SummaryPath);
            Debug.Log("BALL_DONE rows=" + contactRows.ToString(CultureInfo.InvariantCulture)
                + " stayedStanding=" + (standOk ? "true" : "false")
                + " locoStateMax=" + locoStateMax.ToString(CultureInfo.InvariantCulture));
        }
        catch (Exception e)
        {
            Debug.LogError("BallImpactRecorder: writing results failed: " + e.Message);
        }
    }

    private static string J(string value)
    {
        return "\"" + (value ?? string.Empty).Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
    }
}
