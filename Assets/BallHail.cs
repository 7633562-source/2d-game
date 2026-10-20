using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

// ─── LINE 2 — BALL HAIL (the owner's order, 27.09.2026) ───
//
// "Open a second line: tennis balls fly INTO the human while he STANDS; they hit
//  different points of his body at different angles; check the restitution
//  (bounce) coefficients." (Yury, 27.09 ~16:3x)
//
// The balls are IMPACTS, not knockdowns: one real Rigidbody2D per shot with a real
// CircleCollider2D and its OWN PhysicsMaterial2D (honest constants - the audit
// compares the measured restitution against them). Nothing here touches a human
// joint, muscle or rigidbody: the only thing that reaches the man is the solver's
// own collision response, exactly the BALANCE-ARENA-1 contract (ImpactThrower.cs).
//
// STRICTLY 2D: every flight is inside the X-Y plane (angular damping 0, gravity
// along Y only, no out-of-plane force). The man faces +X, so "front" arrivals
// travel in -X.
//
// The shot table is explicit and deterministic: "point:angle:speed" triples on the
// command line (-ballShots), fired at STEP-COUNTED times. No seed, no jitter: one
// take IS its parameter list.
//
// Contact facts (the audit's raw rows) are captured by the ball itself:
//   * the pre-step velocity is read at the START of every fixed step - before the
//     solver and before the collision callbacks of that step (the ImpactThrower
//     lesson: Collision2D.relativeVelocity collapses to the post-hit residual on a
//     class that leaves the step moving together);
//   * the post-step velocity is read inside the collision callback (Unity 2D
//     dispatches collision callbacks after the physics step);
//   * the contact normal comes from the engine's own ContactPoint2D, oriented so
//     it faces the ball's incoming motion (v_in . n <= 0).
//
// Points (with an optional L/R member): head | neck | chest/torso | pelvis/hips |
// shoulder/arm | elbow | hand | thigh | shin/knee | foot/heel | toe.
// Angles: front (horizontal, from his front) | oblique / obliqueL (descending from
// the front-top, 35 deg below horizontal) | obliqueR (rising from the front-bottom,
// 35 deg above horizontal) | fronttop (steep, 55 deg below horizontal) |
// frontbottom (steep from below) | top-down (vertically above) | back (horizontal,
// from behind).
public class BallHailShooter : MonoBehaviour
{
    [Header("The ball - tennis-ball constants, written down for the audit")]
    public float radiusMeters = 0.0335f;   // ITF ball 6.54-6.86 cm diameter
    public float massKg = 0.0577f;         // ITF ball 56.0-59.4 g
    public float bounciness = 0.75f;       // the ball's own restitution (config)
    public float friction = 0.4f;

    [Header("The shot table")]
    public string shotsText = "";
    public float firstShotAt = 6f;         // s, after the stand settles
    public float spacingSeconds = 3f;      // s between the table's shots
    public float spawnDistance = 2.5f;     // m stand-off along the approach line
    public float defaultSpeedMps = 5f;     // used when a triple omits the speed
    public int seed = 1;                   // reserved: the table itself is exact

    [Header("Flight")]
    public float gravityScale = 0f;        // 0 = exactly on the aimed line
    public float lifetimeSeconds = 6f;

    [Header("The human's own contact materials (audit axis)")]
    // < 0 = leave the built rig's materials alone (the honest default: the stand's
    // soles and palm keep their grip). >= 0 assigns one declared PhysicsMaterial2D
    // to every other segment collider, so the audit can run the bounciness pairs
    // (ball e vs segment e) on purpose.
    public float segmentBounciness = -1f;
    public float segmentFriction = 0.4f;

    [Header("Aim fix (Stage 2) - the line-of-sight pre-check")]
    // The sagittal rig occludes itself: a hanging forearm crosses the thigh's line,
    // the head sits forward of the shin's. Before a ball is created the shot's own
    // line is tested against the rig and the aim point slides along the line's normal
    // to the SMALLEST offset whose FIRST solid hit is the intended segment.
    // Deterministic (the same pose gives the same offset) and read-only for the
    // physics: only the spawn line changes. Every row carries what the check saw.
    public bool aimFix = true;
    public float aimFixRange = 0.12f;   // m - the largest slide tried
    public float aimFixStep = 0.01f;    // m
    // b3 (27.09): the same pre-check, but swept with the ball's OWN radius. A
    // zero-width ray slipped past the right toe in b2 while the 33.5 mm ball's
    // edge caught it; the circle cast judges the corridor the ball really flies.
    // false = the b2 ray, kept for the A/B that prices the change.
    public bool aimCircleCast = true;
    // ─── b4 (27.09): THE REACHABILITY FIX (additive; the b3 values keep b3 rows) ───
    // The b3 pre-check slides the aim point along ONE axis (the line's normal). A point
    // buried behind a neighbour needs the aim to move in the plane, not across the line:
    // aimSearchRange > 0 searches a DISC of that radius around the segment's anchor
    // (step = aimFixStep), smallest offset first, so the ball is aimed at the segment's
    // EXPOSED part instead of its centre. 0 = exactly the b3 behaviour.
    public float aimSearchRange = 0f;
    // The one-shot geometry dump of the stand ("" = off): one CSV row per segment at
    // the first shot, so an unreachable point can be reasoned about from measured
    // boxes instead of guessed. Written once; the audit keeps it beside the rows.
    public string rigDumpPath = "";

    [Header("Wiring")]
    public Human target;
    public BallImpactRecorder recorder;

    private readonly List<BallShotPlan> plan = new List<BallShotPlan>();
    private readonly List<BallHailProjectile> live = new List<BallHailProjectile>();
    private bool configured;
    private bool rigDumped;

    public int FiredCount { get; private set; }
    public bool SegmentMaterialsApplied { get; private set; }
    public IReadOnlyList<BallShotPlan> Plan { get { return plan; } }

    // One row of the take's own table.
    public struct BallShotPlan
    {
        public int index;
        public string pointText;      // raw, as written on the command line
        public string angleText;      // raw
        public string part;           // the human segment object, resolved
        public string angleName;      // normalised angle class
        public Vector2 direction;     // unit, the approach's own travel direction
        public float speedMps;
        public float distanceM;       // m, the shot's own stand-off (0 = the global spawnDistance)
        public float timeSeconds;
        public bool fired;
        public float aimShift;        // m, the pre-check's slide along the line's normal
        public float aimDx;           // m, the aim point's offset from the segment anchor (x)
        public float aimDy;           // m, the aim point's offset from the segment anchor (y)
        public string aimPreHit;      // the pre-check's first solid hit at the fired line
        public bool aimOccluded;      // true = no offset cleared; the raw line was fired
    }

    public void Configure(Human human)
    {
        target = human;
        plan.Clear();
        ParseTable();
        ApplySegmentMaterials();
        configured = true;
    }

    // ─── THE TABLE ───
    // "point:angle:speed" triples separated by ';' or ','. The speed is optional
    // (default defaultSpeedMps); the point and the angle are not.
    private void ParseTable()
    {
        string text = shotsText ?? string.Empty;
        string[] tokens = text.Split(new[] { ';', ',' }, System.StringSplitOptions.RemoveEmptyEntries);
        int index = 0;
        for (int i = 0; i < tokens.Length; i++)
        {
            string token = tokens[i].Trim();
            if (token.Length == 0 || token.StartsWith("#"))
                continue;
            string[] fields = token.Split(':');
            if (fields.Length < 2)
            {
                Debug.LogWarning("BallHail: shot token '" + token + "' is not point:angle[:speed] - skipped.");
                continue;
            }
            string pointText = fields[0].Trim();
            string angleText = fields[1].Trim();
            float speed = defaultSpeedMps;
            if (fields.Length > 2 && !float.TryParse(fields[2].Trim(), NumberStyles.Float,
                    CultureInfo.InvariantCulture, out speed))
            {
                Debug.LogWarning("BallHail: speed '" + fields[2] + "' in '" + token + "' unreadable - default used.");
                speed = defaultSpeedMps;
            }
            // b4: the optional 4th field is the shot's own stand-off in metres (0 and a
            // missing field both mean the global spawnDistance). A low point reaches out
            // of the head's own column only from a closer muzzle; the rest of the table
            // is untouched by this.
            float distance = 0f;
            if (fields.Length > 3 && !float.TryParse(fields[3].Trim(), NumberStyles.Float,
                    CultureInfo.InvariantCulture, out distance))
            {
                Debug.LogWarning("BallHail: stand-off '" + fields[3] + "' in '" + token + "' unreadable - global used.");
                distance = 0f;
            }

            Vector2 direction;
            int angleSide;
            string angleName;
            if (!TryAngle(angleText, out direction, out angleSide, out angleName))
            {
                Debug.LogWarning("BallHail: unknown angle '" + angleText + "' in '" + token + "' - skipped.");
                continue;
            }
            string part = ResolvePartName(pointText, angleSide);
            if (string.IsNullOrEmpty(part))
            {
                Debug.LogWarning("BallHail: unknown contact point '" + pointText + "' in '" + token + "' - skipped.");
                continue;
            }

            BallShotPlan shot = new BallShotPlan();
            shot.index = index;
            shot.pointText = pointText;
            shot.angleText = angleText;
            shot.part = part;
            shot.angleName = angleName;
            shot.direction = direction;
            shot.speedMps = speed;
            shot.distanceM = distance;
            shot.timeSeconds = firstShotAt + index * spacingSeconds;
            shot.fired = false;
            plan.Add(shot);
            index++;
        }
    }

    private static string SideName(int side)
    {
        return side > 0 ? "Left" : "Right";
    }

    private static bool IsPairBase(string head)
    {
        switch (head)
        {
            case "shoulder": case "arm": case "armupper": case "upperarm":
            case "elbow": case "lowerarm": case "forearm": case "armlower":
            case "hand": case "palm":
            case "thigh": case "shin": case "calf": case "foot": case "heel":
            case "toe": case "toes": case "mtp":
                return true;
            default:
                return false;
        }
    }

    // The segment object's own name (Human.CreateSegment), so a row's segment
    // column is the rig's own word. side: +1 = his left, -1 = his right; an
    // explicit suffix on the point name beats the angle class's side.
    private static string ResolvePartName(string raw, int angleSide)
    {
        string point = (raw ?? string.Empty).Trim().ToLowerInvariant();
        int side = angleSide;
        if (point.EndsWith("left"))
        {
            side = 1;
            point = point.Substring(0, point.Length - 4);
        }
        else if (point.EndsWith("right"))
        {
            side = -1;
            point = point.Substring(0, point.Length - 5);
        }
        else if (point.EndsWith("-l") || point.EndsWith("_l"))
        {
            side = 1;
            point = point.Substring(0, point.Length - 2);
        }
        else if (point.EndsWith("-r") || point.EndsWith("_r"))
        {
            side = -1;
            point = point.Substring(0, point.Length - 2);
        }
        else if (point.Length > 1)
        {
            string last = point.Substring(point.Length - 1);
            string head = point.Substring(0, point.Length - 1);
            if ((last == "l" || last == "r") && IsPairBase(head))
            {
                side = last == "l" ? 1 : -1;
                point = head;
            }
        }

        switch (point)
        {
            case "head": return "Head";
            case "neck": return "Neck";
            case "chest": case "torso": case "trunk": case "body": return "Torso";
            case "pelvis": case "hip": case "hips": case "pelvic": return "Pelvis";
            case "shoulder": case "arm": case "armupper": case "upperarm":
                return SideName(side) + "ArmUpper";
            case "elbow": case "lowerarm": case "forearm": case "armlower":
                return SideName(side) + "ArmLower";
            case "hand": case "palm": return SideName(side) + "ArmHand";
            case "thigh": return SideName(side) + "LegThigh";
            case "shin": case "knee": case "calf": return SideName(side) + "LegShin";
            case "foot": case "heel": return SideName(side) + "LegFoot";
            case "toe": case "toes": case "mtp": return SideName(side) + "LegToe";
            default: return null;
        }
    }

    // The approach direction: the direction the ball TRAVELS. side_out is the
    // default pair member the class picks when the point name carries none.
    private static bool TryAngle(string raw, out Vector2 direction, out int sideOut, out string name)
    {
        string angle = (raw ?? string.Empty).Trim().ToLowerInvariant();
        sideOut = 1;
        // 35 deg and 55 deg, the same two diagonals the table's own comment names.
        const float c35 = 0.81915204f, s35 = 0.57357644f;
        const float c55 = 0.57357644f, s55 = 0.81915204f;
        switch (angle)
        {
            case "front": case "frontlevel": case "level":
                direction = new Vector2(-1f, 0f); name = "front"; return true;
            case "back": case "behind":
                direction = new Vector2(1f, 0f); name = "back"; return true;
            case "topdown": case "top": case "drop": case "above": case "down":
                direction = new Vector2(0f, -1f); name = "topdown"; return true;
            case "oblique": case "obliquel": case "oblique-left": case "obliqueleft":
                direction = new Vector2(-c35, -s35); sideOut = 1; name = "obliqueL"; return true;
            case "obliquer": case "oblique-right": case "obliqueright":
                direction = new Vector2(-c35, s35); sideOut = -1; name = "obliqueR"; return true;
            case "fronttop": case "topfront": case "steep":
                direction = new Vector2(-c55, -s55); name = "fronttop"; return true;
            case "frontbottom": case "frontlow": case "rise": case "bounce":
                direction = new Vector2(-c55, s55); name = "frontbottom"; return true;
            // ─── b4: THE MIRROR QUADRANTS (additive; the classes above are untouched) ───
            // The b3 sweep ran out of approach lines for the buried points: the same two
            // diagonals and the vertical, arriving from BEHIND and from BELOW. A point
            // shadowed for one quadrant may stand clear in another; the row records the
            // class that fired, so the audit reads which quadrant reached which point.
            case "backtop": case "topback":
                direction = new Vector2(c35, -s35); name = "backtop"; return true;
            case "backlow": case "lowback": case "riseback":
                direction = new Vector2(c35, s35); name = "backlow"; return true;
            case "backtopsteep": case "steepback":
                direction = new Vector2(c55, -s55); name = "backtopsteep"; return true;
            case "backlowsteep": case "risebacksteep":
                direction = new Vector2(c55, s55); name = "backlowsteep"; return true;
            case "bottomup": case "up":
                direction = new Vector2(0f, 1f); name = "bottomup"; return true;
            default:
                direction = new Vector2(-1f, 0f); name = ""; return false;
        }
    }

    // ─── THE HUMAN'S OWN MATERIALS (the audit axis, off by default) ───
    private void ApplySegmentMaterials()
    {
        SegmentMaterialsApplied = false;
        if (segmentBounciness < 0f || target == null)
            return;
        HumanSegment[] segments = target.GetComponentsInChildren<HumanSegment>(true);
        PhysicsMaterial2D material = new PhysicsMaterial2D("BallHailSegment");
        material.friction = Mathf.Max(0f, segmentFriction);
        material.bounciness = Mathf.Clamp01(segmentBounciness);
        for (int i = 0; i < segments.Length; i++)
        {
            HumanSegment segment = segments[i];
            if (segment == null || segment.collider == null)
                continue;
            // The sole and the palm own the stand's own grip (Human.SoleMaterial /
            // PalmMaterial); they are left alone on purpose, and the summary says so.
            string partName = segment.gameObject.name;
            if (partName.Contains("Foot") || partName.Contains("Toe") || partName.Contains("Hand"))
                continue;
            segment.collider.sharedMaterial = material;
        }
        SegmentMaterialsApplied = true;
    }

    public bool SegmentOwnsItsMaterial(string partName)
    {
        return partName.Contains("Foot") || partName.Contains("Toe") || partName.Contains("Hand");
    }

    // ─── ONE SIMULATED STEP ───
    // Called once per executed physics step by the trial harness, exactly as the
    // impact thrower's own Advance is: nothing here reads a frame or a wall clock.
    public void Advance(float time)
    {
        if (!configured)
            return;
        // b4: the stand's measured geometry, once, just before the first ball flies -
        // the pose has settled by then, so the dump describes the take's own stand.
        if (!rigDumped && rigDumpPath.Length > 0 && time >= firstShotAt)
        {
            rigDumped = true;
            DumpRig();
        }
        for (int i = 0; i < plan.Count; i++)
        {
            BallShotPlan shot = plan[i];
            if (shot.fired || shot.timeSeconds > time)
                continue;
            shot.fired = true;
            plan[i] = Fire(shot);
        }

        for (int i = live.Count - 1; i >= 0; i--)
        {
            BallHailProjectile projectile = live[i];
            if (projectile == null)
            {
                live.RemoveAt(i);
                continue;
            }
            projectile.StepSample(time);
            if (time - projectile.BornTime >= lifetimeSeconds)
            {
                Destroy(projectile.gameObject);
                live.RemoveAt(i);
            }
        }
    }

    public BallHailProjectile ProjectileFor(int shotIndex)
    {
        for (int i = 0; i < live.Count; i++)
        {
            if (live[i] != null && live[i].ShotIndex == shotIndex)
                return live[i];
        }
        return null;
    }

    public bool FiredShot(int index)
    {
        for (int i = 0; i < plan.Count; i++)
            if (plan[i].index == index)
                return plan[i].fired;
        return false;
    }

    public float GroundTopY { get; set; }

    // ─── b4: THE RIG DUMP (measured geometry, not guessed) ───
    // One row per HumanSegment: the anchor, the box, the mass, the world bounds of
    // the collider and its material - plus the ball's own config and the ground line.
    // A point that no approach line can reach is then a solvable geometry problem on
    // paper (which box shadows which), with the same numbers the casts ran against.
    private void DumpRig()
    {
        try
        {
            if (target == null || rigDumpPath.Length == 0)
                return;
            HumanSegment[] segments = target.GetComponentsInChildren<HumanSegment>(true);
            // b5 (lint fix, 28.09): the config line must split into the SAME number of
            // ';' fields as the data rows (12), or Tools/impact-json-lint.py check 6
            // reads the config line as the header and flags every data row (it did on
            // every b4 rigdump). The line therefore ends ";units;SI;segments;N" -
            // information, not dummy cells.
            int segCount = 0;
            for (int i = 0; i < segments.Length; i++)
                if (segments[i] != null)
                    segCount++;
            StringBuilder text = new StringBuilder(8192);
            text.Append("#ball_radius;").Append(radiusMeters.ToString("0.#####", CultureInfo.InvariantCulture));
            text.Append(";ball_mass;").Append(massKg.ToString("0.#####", CultureInfo.InvariantCulture));
            text.Append(";standoff;").Append(spawnDistance.ToString("0.####", CultureInfo.InvariantCulture));
            text.Append(";groundTopY;").Append(GroundTopY.ToString("0.#####", CultureInfo.InvariantCulture));
            text.Append(";units;SI;segments;").Append(segCount.ToString(CultureInfo.InvariantCulture));
            text.AppendLine();
            text.AppendLine("name;x;y;rotZ;sizeX;sizeY;mass;minX;maxX;minY;maxY;material");
            for (int i = 0; i < segments.Length; i++)
            {
                HumanSegment segment = segments[i];
                if (segment == null)
                    continue;
                Vector3 p = segment.transform.position;
                Vector2 size = segment.size;
                float minX = float.NaN, maxX = float.NaN, minY = float.NaN, maxY = float.NaN;
                string material = "none";
                if (segment.collider != null)
                {
                    Bounds b = segment.collider.bounds;
                    minX = b.min.x; maxX = b.max.x; minY = b.min.y; maxY = b.max.y;
                    material = segment.collider.sharedMaterial != null
                        ? segment.collider.sharedMaterial.bounciness.ToString("0.####", CultureInfo.InvariantCulture)
                        : "none";
                }
                text.Append(segment.gameObject.name).Append(';');
                text.Append(p.x.ToString("0.#####", CultureInfo.InvariantCulture)).Append(';');
                text.Append(p.y.ToString("0.#####", CultureInfo.InvariantCulture)).Append(';');
                text.Append(segment.transform.eulerAngles.z.ToString("0.####", CultureInfo.InvariantCulture)).Append(';');
                text.Append(size.x.ToString("0.#####", CultureInfo.InvariantCulture)).Append(';');
                text.Append(size.y.ToString("0.#####", CultureInfo.InvariantCulture)).Append(';');
                text.Append(segment.mass.ToString("0.#####", CultureInfo.InvariantCulture)).Append(';');
                text.Append(minX.ToString("0.#####", CultureInfo.InvariantCulture)).Append(';');
                text.Append(maxX.ToString("0.#####", CultureInfo.InvariantCulture)).Append(';');
                text.Append(minY.ToString("0.#####", CultureInfo.InvariantCulture)).Append(';');
                text.Append(maxY.ToString("0.#####", CultureInfo.InvariantCulture)).Append(';');
                text.Append(material);
                text.AppendLine();
            }
            File.WriteAllText(rigDumpPath, text.ToString(), new UTF8Encoding(false));
            Debug.Log("BALL_RIGDUMP " + rigDumpPath + " segments=" + segments.Length.ToString(CultureInfo.InvariantCulture));
        }
        catch (Exception e)
        {
            Debug.LogError("BallHail: rig dump failed: " + e.Message);
        }
    }

    // ─── ONE SHOT ───
    private BallShotPlan Fire(BallShotPlan shot)
    {
        Transform part = target != null ? target.transform.Find(shot.part) : null;
        if (part == null)
        {
            Debug.LogWarning("BallHail: shot " + shot.index.ToString(CultureInfo.InvariantCulture)
                + " - the rig has no segment '" + shot.part + "' (not fired).");
            return shot;
        }

        Vector2 anchor = part.position;
        Vector2 aim = anchor;
        Vector2 direction = shot.direction;
        // b4: the shot may carry its own stand-off (a low point under the head's own
        // column needs a closer muzzle); 0 keeps the take's global distance.
        float standOff = shot.distanceM > 0f ? shot.distanceM : spawnDistance;
        Vector2 launch = aim - direction * standOff;
        // Never launch from under the floor: if the aimed quadrant would put the
        // muzzle below the stance line, the muzzle is lifted to the stance line and
        // the ball is re-aimed at the segment. The approach QUADRANT is kept; the
        // angle a row reports is the measured one, never what the table said.
        float minY = GroundTopY + radiusMeters + 0.02f;
        if (launch.y < minY)
            launch = new Vector2(launch.x, minY);

        // Stage 2: the line-of-sight pre-check. It moves the AIM point only - the
        // launch stays on the class's own approach line, so a take stays comparable
        // shot for shot. A shot whose raw line was clear keeps shift 0 and its row
        // reads exactly like a b1 row.
        string preHit;
        Vector2 aimOffset;
        aim = AimFix(launch, aim, shot.part, out shot.aimShift, out aimOffset, out preHit, out shot.aimOccluded);
        shot.aimPreHit = preHit;
        shot.aimDx = aimOffset.x;
        shot.aimDy = aimOffset.y;

        Vector2 toAim = aim - launch;
        if (toAim.sqrMagnitude < 1e-6f)
            toAim = -direction;
        Vector2 velocity = toAim.normalized * shot.speedMps;

        GameObject go = new GameObject("Ball_" + shot.index.ToString(CultureInfo.InvariantCulture)
            + "_" + shot.part);
        go.transform.position = new Vector3(launch.x, launch.y, 0f);

        Rigidbody2D body = go.AddComponent<Rigidbody2D>();
        body.mass = massKg;
        body.gravityScale = gravityScale;
        body.linearDamping = 0f;
        body.angularDamping = 0f;
        // 4-20 m/s through a 0.1 m limb: without continuous detection the solver can
        // step over a thin segment box and the "impact" would never happen.
        body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        body.interpolation = RigidbodyInterpolation2D.None;
        body.linearVelocity = velocity;

        // The ball's OWN material: friction and bounciness are the audit's config
        // column, never the engine default (project rule, collider-materials).
        PhysicsMaterial2D material = new PhysicsMaterial2D("TennisBall");
        material.friction = Mathf.Max(0f, friction);
        material.bounciness = Mathf.Clamp01(bounciness);
        CircleCollider2D circle = go.AddComponent<CircleCollider2D>();
        circle.radius = Mathf.Max(0.005f, radiusMeters);
        circle.sharedMaterial = material;

        SpriteRenderer renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = DiscSprite(radiusMeters);
        renderer.color = new Color(0.86f, 0.97f, 0.12f, 1f);
        renderer.drawMode = SpriteDrawMode.Simple;
        renderer.sortingOrder = 900;

        BallHailProjectile projectile = go.AddComponent<BallHailProjectile>();
        projectile.Configure(this, recorder, shot.index, shot.pointText, shot.part, shot.angleName,
            aim, launch, velocity, shot.speedMps, target, shot.aimShift, shot.aimPreHit, shot.aimOccluded,
            standOff, aimOffset);
        live.Add(projectile);
        FiredCount++;
        return shot;
    }

    // ─── THE AIM FIX (Stage 2): the first thing on the line must be the aim ───
    // The candidates are ordered by |offset| (0 first, then +step/-step, +2step/-2step
    // ...), so the smallest slide that clears is the one taken: the least the shot is
    // moved from the class's own line. Nothing here writes to the world - a ray and a
    // local variable only. If no candidate clears, the RAW line is fired and the row
    // says aimOccluded, so an occluded point is visible, never silently "fixed".
    private Vector2 AimFix(Vector2 launch, Vector2 aim, string part, out float shift,
        out Vector2 appliedOffset, out string firstHitName, out bool occluded)
    {
        shift = 0f;
        appliedOffset = Vector2.zero;
        firstHitName = string.Empty;
        occluded = false;
        if (!aimFix)
            return aim;

        Vector2 anchor = aim;                 // b4: the offset is measured from here
        Vector2 baseVector = aim - launch;
        float length = baseVector.magnitude;
        if (length < 1e-4f)
            return aim;
        Vector2 direction = baseVector / length;
        Vector2 normal = new Vector2(-direction.y, direction.x);
        int steps = Mathf.Max(0, Mathf.RoundToInt(aimFixRange / Mathf.Max(0.001f, aimFixStep)));

        for (int k = 0; k <= steps; k++)
        {
            int sides = k == 0 ? 1 : 2;
            for (int s = 0; s < sides; s++)
            {
                float offset = k == 0 ? 0f : (s == 0 ? k * aimFixStep : -k * aimFixStep);
                Vector2 candidate = aim + normal * offset;
                Vector2 toCandidate = candidate - launch;
                float distance = toCandidate.magnitude;
                if (distance < 1e-4f)
                    continue;
                string hit = aimCircleCast
                    ? FirstSolidCircleHit(launch, toCandidate / distance, distance + 0.5f, radiusMeters)
                    : FirstSolidHit(launch, toCandidate / distance, distance + 0.5f);
                if (k == 0)
                    firstHitName = hit;            // what the class's own line really meets
                if (hit == part)
                {
                    shift = offset;
                    appliedOffset = candidate - anchor;
                    firstHitName = hit;
                    if (offset != 0f)
                        Debug.Log("BALL_AIMFIX part=" + part + " shift="
                            + offset.ToString("0.###", CultureInfo.InvariantCulture)
                            + " cast=" + (aimCircleCast ? "1" : "0"));
                    return candidate;
                }
            }
        }

        // ─── b4: THE DISC SEARCH ───
        // The one-axis slide cannot free a point that a NEIGHBOUR shadows in the plane
        // (a shoulder under the torso's own edge, a thigh behind the hanging forearm):
        // the aim has to move sideways to the segment's exposed part. Rings of the disc
        // are tried smallest radius first, so the shot stays as close to the class's own
        // line as the geometry allows. Read-only, like the slide: a cast and a variable.
        if (aimSearchRange > 0.001f)
        {
            int searchSteps = Mathf.Max(1, Mathf.RoundToInt(aimSearchRange / Mathf.Max(0.001f, aimFixStep)));
            for (int ring = 1; ring <= searchSteps; ring++)
            {
                float radius = ring * aimFixStep;
                int ringSides = Mathf.Max(8, Mathf.RoundToInt(2f * Mathf.PI * radius / Mathf.Max(0.001f, aimFixStep)));
                for (int a = 0; a < ringSides; a++)
                {
                    float angle = 2f * Mathf.PI * a / ringSides;
                    Vector2 candidate = anchor + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                    Vector2 toCandidate = candidate - launch;
                    float distance = toCandidate.magnitude;
                    if (distance < 1e-4f)
                        continue;
                    string hit = aimCircleCast
                        ? FirstSolidCircleHit(launch, toCandidate / distance, distance + 0.5f, radiusMeters)
                        : FirstSolidHit(launch, toCandidate / distance, distance + 0.5f);
                    if (hit == part)
                    {
                        appliedOffset = candidate - anchor;
                        shift = Vector2.Dot(appliedOffset, normal);
                        firstHitName = hit;
                        Debug.Log("BALL_AIMSEARCH part=" + part + " d="
                            + radius.ToString("0.###", CultureInfo.InvariantCulture)
                            + " dx=" + appliedOffset.x.ToString("0.###", CultureInfo.InvariantCulture)
                            + " dy=" + appliedOffset.y.ToString("0.###", CultureInfo.InvariantCulture)
                            + " cast=" + (aimCircleCast ? "1" : "0"));
                        return candidate;
                    }
                }
            }
        }

        occluded = true;
        Debug.LogWarning("BALL_AIMFIX part=" + part + " OCCLUDED by '" + firstHitName
            + "' within " + aimFixRange.ToString("0.###", CultureInfo.InvariantCulture) + " m"
            + " search=" + aimSearchRange.ToString("0.###", CultureInfo.InvariantCulture)
            + " cast=" + (aimCircleCast ? "1" : "0"));
        return aim;
    }

    // The first non-trigger collider's own object name along a ray ("" = clear sky).
    private static string FirstSolidHit(Vector2 origin, Vector2 direction, float distance)
    {
        RaycastHit2D[] hits = Physics2D.RaycastAll(origin, direction, distance);
        string name = string.Empty;
        float best = float.MaxValue;
        for (int i = 0; i < hits.Length; i++)
        {
            RaycastHit2D hit = hits[i];
            if (hit.collider == null || hit.collider.isTrigger)
                continue;
            if (hit.distance < best)
            {
                best = hit.distance;
                name = hit.collider.transform.name;
            }
        }
        return name;
    }

    // b3: the first-solid query swept with the ball's own width (Physics2D.CircleCastAll).
    // Same filter as the ray: a trigger does not count, the nearest solid surface wins;
    // the circle's edge touching a neighbour is a hit even when the centre line is clear.
    private static string FirstSolidCircleHit(Vector2 origin, Vector2 direction, float distance, float radius)
    {
        RaycastHit2D[] hits = Physics2D.CircleCastAll(origin, Mathf.Max(0.001f, radius), direction, distance);
        string name = string.Empty;
        float best = float.MaxValue;
        for (int i = 0; i < hits.Length; i++)
        {
            RaycastHit2D hit = hits[i];
            if (hit.collider == null || hit.collider.isTrigger)
                continue;
            if (hit.distance < best)
            {
                best = hit.distance;
                name = hit.collider.transform.name;
            }
        }
        return name;
    }

    private Sprite discSprite;
    private float discSpriteRadius = -1f;

    // One disc, sized in metres by the sprite's own pixels-per-unit (a scaled
    // transform would scale the collider with it - the collider stays untouched).
    private Sprite DiscSprite(float radius)
    {
        if (discSprite != null && Mathf.Abs(discSpriteRadius - radius) < 1e-6f)
            return discSprite;
        const int size = 32;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        float center = size * 0.5f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x + 0.5f - center;
                float dy = y + 0.5f - center;
                float distance = Mathf.Sqrt(dx * dx + dy * dy);
                float alpha = Mathf.Clamp01(center - distance);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }
        tex.Apply(false, false);
        float ppu = size / Mathf.Max(0.001f, radius * 2f);
        discSprite = Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), ppu);
        discSpriteRadius = radius;
        return discSprite;
    }
}

// ─── THE BALL ───
// One per shot. It carries the table's own numbers and the MEASURED contact facts:
// the pre-step velocity of the step that met the surface, the post-step velocity
// the solver left behind, the contact point and the contact normal.
public class BallHailProjectile : MonoBehaviour
{
    public int ShotIndex { get; private set; }
    public string PointText { get; private set; }
    public string AimedPart { get; private set; }
    public string AngleName { get; private set; }
    public Vector2 AimPoint { get; private set; }
    public Vector2 LaunchPoint { get; private set; }
    public Vector2 LaunchVelocity { get; private set; }
    public float AimShiftM { get; private set; }
    public float StandoffM { get; private set; }      // b4: the shot's own muzzle distance
    public Vector2 AimOffset { get; private set; }    // b4: the aim point vs the segment anchor
    public string AimPreHit { get; private set; }
    public bool AimOccluded { get; private set; }
    public float PlannedSpeedMps { get; private set; }
    public float BornTime { get; private set; }
    public int Contacts { get; private set; }
    public float MinAimDistanceM { get; private set; }
    public bool EverHitHuman { get; private set; }

    // The velocity entering the solver this step: read in FixedUpdate, which runs
    // before the solver and before this step's collision callbacks.
    public Vector2 PreStepVelocity { get; private set; }

    private BallHailShooter owner;
    private BallImpactRecorder recorder;
    private Rigidbody2D body;
    private float lastTime;
    private Rigidbody2D[] parts = new Rigidbody2D[0];
    private readonly Dictionary<Rigidbody2D, Vector2> partVelocity =
        new Dictionary<Rigidbody2D, Vector2>(64);

    public void Configure(BallHailShooter shooter, BallImpactRecorder target, int shotIndex,
        string pointText, string aimedPart, string angleName, Vector2 aim, Vector2 launch,
        Vector2 velocity, float plannedSpeed, Human human, float aimShift, string aimPreHit,
        bool aimOccluded, float standoff = 0f, Vector2 aimOffset = default(Vector2))
    {
        owner = shooter;
        recorder = target;
        ShotIndex = shotIndex;
        PointText = pointText;
        AimedPart = aimedPart;
        AngleName = angleName;
        AimPoint = aim;
        LaunchPoint = launch;
        LaunchVelocity = velocity;
        PlannedSpeedMps = plannedSpeed;
        AimShiftM = aimShift;
        StandoffM = standoff;
        AimOffset = aimOffset;
        AimPreHit = aimPreHit;
        AimOccluded = aimOccluded;
        BornTime = -1f;
        Contacts = 0;
        MinAimDistanceM = float.MaxValue;
        body = GetComponent<Rigidbody2D>();
        PreStepVelocity = velocity;
        parts = CollectPartBodies(human);
    }

    // Called once per executed step by the shooter: the miss-distance bookkeeping
    // and the ball's own age. Read-only - nothing here can be seen by the solver.
    public void StepSample(float time)
    {
        if (BornTime < 0f)
            BornTime = time;
        lastTime = time;
        if (body == null)
            return;
        float distance = Vector2.Distance(body.position, AimPoint);
        if (distance < MinAimDistanceM)
            MinAimDistanceM = distance;
    }

    public float AgeSeconds { get { return lastTime - BornTime; } }

    private void FixedUpdate()
    {
        // Start of the step: the velocity entering the solver, i.e. the one the
        // contact of this step will be solved against.
        if (body != null)
            PreStepVelocity = body.linearVelocity;
        for (int i = 0; i < parts.Length; i++)
        {
            Rigidbody2D part = parts[i];
            if (part != null)
                partVelocity[part] = part.linearVelocity;
        }
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision == null || collision.collider == null || body == null)
            return;
        if (recorder == null)
            return;

        Human other = collision.collider.GetComponentInParent<Human>();
        ContactPoint2D contact = collision.GetContact(0);
        Vector2 normal = contact.normal;
        Vector2 velocityIn = PreStepVelocity;
        Vector2 velocityOut = body.linearVelocity;   // post-step: callbacks run after the solve
        // The normal always faces the ball's incoming motion, so v_in . n <= 0 and
        // the restitution is a positive ratio along one declared axis.
        if (Vector2.Dot(velocityIn, normal) > 0f)
            normal = -normal;

        Vector2 segmentVelocityIn = Vector2.zero;
        Vector2 segmentVelocityOut = Vector2.zero;
        Rigidbody2D otherBody = collision.rigidbody;
        if (otherBody != null)
        {
            if (partVelocity.TryGetValue(otherBody, out Vector2 cached))
                segmentVelocityIn = cached;
            segmentVelocityOut = otherBody.linearVelocity;
        }

        PhysicsMaterial2D otherMaterial = collision.collider.sharedMaterial;
        float otherBounciness = otherMaterial != null ? otherMaterial.bounciness : -1f;

        BallContactInfo info = new BallContactInfo();
        info.ballId = ShotIndex;
        info.pointText = PointText;
        info.partAimed = AimedPart;
        info.angleName = AngleName;
        info.speedPlanned = PlannedSpeedMps;
        info.time = lastTime;
        info.segment = collision.collider.transform.name;
        info.isHuman = other != null;
        info.hitPoint = contact.point;
        info.normal = normal;
        info.velocityIn = velocityIn;
        info.velocityOut = velocityOut;
        info.segmentVelocityIn = segmentVelocityIn;
        info.segmentVelocityOut = segmentVelocityOut;
        info.engineRelativeSpeed = collision.relativeVelocity.magnitude;
        info.ballBounciness = owner != null ? owner.bounciness : -1f;
        info.segmentBounciness = otherBounciness;
        info.ballMass = owner != null ? owner.massKg : -1f;
        info.contactsSoFar = Contacts;
        info.aimShift = AimShiftM;
        info.aimDx = AimOffset.x;
        info.aimDy = AimOffset.y;
        info.standoff = StandoffM;
        info.aimPreHit = AimPreHit;
        info.aimOccluded = AimOccluded;

        Contacts++;
        if (other != null)
            EverHitHuman = true;
        recorder.OnContact(info);
    }

    // The human's own rigidbodies, one per segment: the relative restitution needs
    // the segment's velocity beside the ball's. Read-only.
    private static Rigidbody2D[] CollectPartBodies(Human target)
    {
        if (target == null) return new Rigidbody2D[0];
        HumanSegment[] segments = target.GetComponentsInChildren<HumanSegment>(true);
        List<Rigidbody2D> bodies = new List<Rigidbody2D>(segments.Length);
        for (int i = 0; i < segments.Length; i++)
        {
            HumanSegment segment = segments[i];
            if (segment != null && segment.rb != null && !bodies.Contains(segment.rb))
                bodies.Add(segment.rb);
        }
        return bodies.ToArray();
    }
}

// The raw facts of one contact, handed to the recorder.
public struct BallContactInfo
{
    public int ballId;
    public string pointText;
    public string partAimed;
    public string angleName;
    public float speedPlanned;
    public float time;
    public string segment;
    public bool isHuman;
    public Vector2 hitPoint;
    public Vector2 normal;
    public Vector2 velocityIn;
    public Vector2 velocityOut;
    public Vector2 segmentVelocityIn;
    public Vector2 segmentVelocityOut;
    public float engineRelativeSpeed;
    public float ballBounciness;
    public float segmentBounciness;
    public float ballMass;        // kg, the ball's own config (the mass ladder's column)
    public int contactsSoFar;
    public float aimShift;        // m, the Stage-2 pre-check's slide for this ball
    public float aimDx;           // m, the aim point vs the segment anchor (b4)
    public float aimDy;           // m, the aim point vs the segment anchor (b4)
    public float standoff;        // m, the shot's own muzzle distance (b4)
    public string aimPreHit;      // the pre-check's first solid hit at the fired line
    public bool aimOccluded;      // true = the raw line was fired (nothing cleared)
}
