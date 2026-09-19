using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

// ARENA-1: one arena human. Owns nothing but the measurement - the body itself is
// the standard rig (Human.BuildHuman), so the numbers stay comparable with the
// walk trials.
//
// Upright / fallen come from the project-wide criterion, Assets/FallCriterion.cs
// (C1, 18.09 - the trial `fell` flag is never read here, and this file no longer
// carries a second copy of the rule):
//   down  = trunk tilt past 60 deg, OR pelvis 0.25 m below THIS human's own
//           SETTLED stand height, sustained 0.15 s
// The stand height is measured over the first second of the human's life, after
// the drop-in has settled (spawn y -0.82 settles to -1.00 within 0.25 s), while
// the body is upright. It used to be the running MAXIMUM, which in the spawn
// pose is 0.186 m above the settled stance - so 0.064 m of ordinary walk-onset
// sink latched `fallen` in 2112 of 2112 rows and the column carried no
// information at all (Trials/Runs/arena1_latch-findings.md).
//
// The trajectory ring (every 0.25 s, first 15 s) exists for one reason: the elite
// genome is re-spawned unchanged, and the two copies must walk the same distance
// and the same early path. If a younger copy is deflected, a human-human collision
// is still live.
public class ArenaHumanAgent : MonoBehaviour
{
    // Kept for the elite-copy / cull reporting: the same physical criterion the
    // shared file applies, named here only so a reader can see the numbers.
    public const float FallTiltDegrees = FallCriterion.TiltDegrees;
    public const float FallDropMeters = FallCriterion.DropMeters;
    public const float FallSustainSeconds = FallCriterion.SustainSeconds;
    private const int TrajectorySlots = 64;
    private const float TrajectoryPeriod = 0.25f;

    // ─── the number on the head (Yury's 18.09 correction) ────────────────────
    // The digit belongs INSIDE the head: centred on the head's own box and sized to
    // a measured fraction of the head's height. `ArenaRunner.numberHeight` is the
    // REQUESTED size, so it acts as a ceiling and never as the target - its
    // Inspector default 0.34 m is larger than a whole head, which is exactly what
    // used to be drawn ABOVE the skull and then merged into the pile in the frame.
    // The correction's ceiling is 60 % of the head height; 0.55 is used so the
    // glyph keeps a margin inside the box.
    public const float NumberHeadHeightFraction = 0.55f;
    public const float NumberMaxFractionOfHead = 0.60f;
    private const int NumberLogLimit = 24;          // log the first humans only
    public static int NumberSpawned;                // numbers attached this run
    public static int NumberLogged;
    public static float NumberLastHeadHeightMeters;
    public static float NumberLastTargetHeightMeters;
    public static float NumberLastDrawnHeightMeters;
    public static bool NumberLastInsideHead;

    private TextMesh numberMesh;
    private float numberTargetHeight;
    private int numberCheckStage;                   // 0 measure+correct, 1 verify
    private float numberHeadHeight;

    // identity / inheritance (provable from the log)
    public int id;
    public int parentId = -1;
    public int childKind;              // 0 fresh random, 1 child of a parent, 2 elite copy
    public int mutationSeed;
    public bool parentInBestQuartile;
    public int parentRank = -1;
    public int poolSize;
    public float parentFitness = float.NaN;

    public float spawnTime;
    public float spawnX;
    public float[] genome = new float[ArenaGenome.Count];

    // measurement
    public bool fallen;
    public float fallTime = -1f;
    public float tiltAtFall;
    public float distanceAt15 = float.NaN;
    public bool uprightAt15;
    public float distanceAtCull = float.NaN;
    public bool uprightAtCull;
    public bool aliveAtCull = true;
    public float destroyedTime = -1f;
    public float measuredTime = -1f;

    private Human human;
    private Rigidbody2D pelvis;
    private VestibularSystem vestibular;
    // C1: the shared criterion's latch and the SETTLED stand height it is measured
    // against. The running maximum that stood here climbed to the spawn pose.
    private FallCriterion.Latch fallLatch;
    private FallCriterion.Baseline standBaseline;
    // The stand height is read from this window of the human's own life: the
    // drop-in has settled by 0.25 s, and at 1.2 s the body is still standing.
    private const float BaselineWindowStart = 0.4f;
    private const float BaselineWindowEnd = 1.2f;
    private float lastTilt;
    private float lastPelvisY;
    private float nextTrajectoryTime;
    private int trajectoryCount;
    private readonly float[] trajectory = new float[TrajectorySlots * 3];
    private float simDelta = 0.005f;

    public float Distance => pelvis != null ? pelvis.position.x - spawnX : 0f;
    public float Tilt => lastTilt;
    public float PelvisY => lastPelvisY;
    public float Age(float simTime) => simTime - spawnTime;
    public Human Body => human;
    public Rigidbody2D PelvisBody => pelvis;

    // Fitness: distance minus a fall penalty (see ArenaGenome.FallPenaltyMeters).
    public float Fitness => (float.IsNaN(distanceAt15) ? 0f : distanceAt15)
        - (fallen ? ArenaGenome.FallPenaltyMeters : 0f);

    public void Bind(Human owner, float spawnTimeSeconds, float spawnXMeters, float delta)
    {
        human = owner;
        spawnTime = spawnTimeSeconds;
        spawnX = spawnXMeters;
        simDelta = delta > 0f ? delta : 0.005f;
        vestibular = owner != null ? owner.vestibularSystem : null;
        if (owner != null)
        {
            Transform body = owner.transform.Find("Pelvis");
            if (body != null) pelvis = body.GetComponent<Rigidbody2D>();
        }
        nextTrajectoryTime = spawnTime;
    }

    // One fixed step of measurement. Called by ArenaRunner, never by physics.
    public void Step(float simTime)
    {
        if (pelvis == null) return;

        CalibrateNumber(simTime);

        lastTilt = Mathf.Abs(vestibular != null ? vestibular.GetBodyTilt() : 0f);
        lastPelvisY = pelvis.position.y;

        // The settled stand height: measured over the first second of this
        // human's life once the drop-in has settled, then frozen. The running
        // maximum that stood here was the spawn pose - 0.186 m above the settled
        // stance - and 0.064 m of ordinary sink then latched the flag.
        float age = simTime - spawnTime;
        if (age >= BaselineWindowStart && age < BaselineWindowEnd)
            standBaseline.Feed(lastPelvisY, lastTilt);
        else if (age >= BaselineWindowEnd)
            standBaseline.Freeze();
        float standHeight = standBaseline.valid
            ? standBaseline.height
            : float.NegativeInfinity;
        // No measured stand height disables the drop arm; the tilt arm still
        // applies. The spawn pose is never used as a bar.
        bool downNow = FallCriterion.Step(ref fallLatch, standHeight, lastPelvisY,
            lastTilt, simDelta, simTime);

        if (!fallen && fallLatch.fired)
        {
            fallen = true;
            fallTime = fallLatch.fireTime;
            tiltAtFall = lastTilt;
        }

        if (trajectoryCount < TrajectorySlots && simTime >= nextTrajectoryTime)
        {
            nextTrajectoryTime += TrajectoryPeriod;
            trajectory[trajectoryCount * 3] = simTime - spawnTime;
            trajectory[trajectoryCount * 3 + 1] = pelvis.position.x - spawnX;
            trajectory[trajectoryCount * 3 + 2] = pelvis.position.y;
            trajectoryCount++;
        }
    }

    // The digit is measured, not assumed: whatever the first frame drew is read
    // back from the rendered mesh, corrected once against the head-relative
    // target, and verified on the following step. The log carries head height,
    // target, drawn height and whether the glyph sits inside the head's own box,
    // so a frame can be checked without an eye.
    private void CalibrateNumber(float simTime)
    {
        if (numberCheckStage >= 2) return;
        if (numberMesh == null)
        {
            ArenaNumberTarget intent = GetComponentInChildren<ArenaNumberTarget>();
            if (intent == null)
            {
                // No number on this human (older path) - nothing to measure.
                if (simTime - spawnTime > 0.5f) numberCheckStage = 2;
                return;
            }
            numberMesh = intent.GetComponent<TextMesh>();
            numberTargetHeight = intent.targetHeightMeters;
            numberHeadHeight = intent.headHeightMeters;
            if (numberMesh == null) { numberCheckStage = 2; return; }
        }

        MeshRenderer rendered = numberMesh.GetComponent<MeshRenderer>();
        float drawn = rendered != null ? rendered.bounds.size.y : 0f;
        if (drawn <= 1e-5f)
        {
            // -nographics has no renderer to read: the analytic value stands, and
            // the log says so instead of pretending it was measured.
            if (simTime - spawnTime > 0.5f && numberCheckStage == 0)
            {
                numberCheckStage = 2;
                ReportNumber(numberTargetHeight, false, false);
            }
            return;
        }

        if (numberCheckStage == 0)
        {
            numberCheckStage = 1;
            if (Mathf.Abs(drawn - numberTargetHeight) > 0.002f)
                numberMesh.characterSize *= numberTargetHeight / drawn;
            return;
        }

        numberCheckStage = 2;
        ReportNumber(drawn, true, NumberInsideHead(numberMesh));
    }

    private void ReportNumber(float drawn, bool measured, bool inside)
    {
        NumberLastHeadHeightMeters = numberHeadHeight;
        NumberLastTargetHeightMeters = numberTargetHeight;
        NumberLastDrawnHeightMeters = drawn;
        NumberLastInsideHead = inside;
        if (NumberLogged >= NumberLogLimit) return;
        NumberLogged++;
        Debug.Log(string.Format(CultureInfo.InvariantCulture,
            "ARENA-NUMBER id={0} head={1:0.000}m target={2:0.000}m drawn={3:0.000}m "
            + "ratio={4:0.00} measured={5} insideHead={6} ceiling={7:0.00}x head",
            id, numberHeadHeight, numberTargetHeight, drawn,
            numberHeadHeight > 1e-4f ? drawn / numberHeadHeight : 0f, measured, inside,
            NumberMaxFractionOfHead));
    }

    private static bool NumberInsideHead(TextMesh mesh)
    {
        if (mesh == null) return false;
        MeshRenderer rendered = mesh.GetComponent<MeshRenderer>();
        Collider2D headBox = mesh.transform.parent != null
            ? mesh.transform.parent.GetComponent<Collider2D>()
            : null;
        if (rendered == null || headBox == null) return false;
        Bounds head = headBox.bounds;
        Bounds digit = rendered.bounds;
        return digit.min.x >= head.min.x - 1e-4f && digit.max.x <= head.max.x + 1e-4f
            && digit.min.y >= head.min.y - 1e-4f && digit.max.y <= head.max.y + 1e-4f;
    }

    public bool UprightNow()
    {
        return !fallen
            && lastTilt <= FallTiltDegrees
            && (!standBaseline.valid
                || lastPelvisY >= standBaseline.height - FallDropMeters);
    }

    public void MarkMeasured(float simTime)
    {
        measuredTime = simTime;
        distanceAt15 = Distance;
        uprightAt15 = UprightNow();
    }

    public void MarkCulled(float simTime, bool upright)
    {
        destroyedTime = simTime;
        distanceAtCull = Distance;
        uprightAtCull = upright;
        aliveAtCull = false;
    }

    public string TrajectoryJson()
    {
        StringBuilder sb = new StringBuilder(256);
        sb.Append('[');
        for (int i = 0; i < trajectoryCount; i++)
        {
            if (i > 0) sb.Append(',');
            sb.Append("{\"t\":");
            sb.Append(trajectory[i * 3].ToString("0.###", CultureInfo.InvariantCulture));
            sb.Append(",\"x\":");
            sb.Append(trajectory[i * 3 + 1].ToString("0.#####", CultureInfo.InvariantCulture));
            sb.Append(",\"y\":");
            sb.Append(trajectory[i * 3 + 2].ToString("0.#####", CultureInfo.InvariantCulture));
            sb.Append('}');
        }
        sb.Append(']');
        return sb.ToString();
    }

    // Distance along the path at a requested age (used to compare the two elite
    // copies at the same age, not at the same wall time).
    public bool TrajectoryAt(float age, out float x, out float y)
    {
        x = 0f;
        y = 0f;
        if (trajectoryCount == 0) return false;
        int best = 0;
        float bestError = float.MaxValue;
        for (int i = 0; i < trajectoryCount; i++)
        {
            float error = Mathf.Abs(trajectory[i * 3] - age);
            if (error < bestError)
            {
                bestError = error;
                best = i;
            }
        }
        x = trajectory[best * 3 + 1];
        y = trajectory[best * 3 + 2];
        return bestError <= 0.13f;
    }

    public int TrajectorySamples => trajectoryCount;

    // ─── the number on the head ────────────────────────────────────────────
    // Digits above the head, one TextMesh per human: no collider, no rigidbody, no
    // effect on physics. Readable at 1x - verified by looking at a frame, not by
    // reading this code.
    private static Font labelFont;
    private static bool labelFontResolved;

    public static Font LabelFont()
    {
        if (!labelFontResolved)
        {
            labelFontResolved = true;
            labelFont = LoadBuiltinFont("LegacyRuntime.ttf");
            if (labelFont == null)
            {
                labelFont = LoadBuiltinFont("Arial.ttf");
                if (labelFont != null)
                    Debug.LogWarning("ARENA_LABEL_FONT fallback Arial.ttf");
            }
            else
            {
                Debug.Log("ARENA_LABEL_FONT LegacyRuntime.ttf");
            }
            if (labelFont == null)
                Debug.LogError("ARENA_LABEL_FONT none - head numbers will not render");
        }
        return labelFont;
    }

    private static Font LoadBuiltinFont(string name)
    {
        try
        {
            return Resources.GetBuiltinResource<Font>(name);
        }
        catch (System.Exception)
        {
            return null;
        }
    }

    // heightMeters is the height of the digit line in world metres. TextMesh maps
    // characterSize * fontSize / 10 to that height for the built-in font, so the
    // character size follows from the requested height.
    public static TextMesh AttachNumber(GameObject humanObject, int number, float heightMeters)
    {
        if (humanObject == null) return null;
        Transform head = humanObject.transform.Find("Head");
        if (head == null) return null;

        // The digit is measured against the head's OWN box, not against the body
        // height, and the requested size is a ceiling: the head is about 0.22 m on
        // this rig, so the requested 0.34 m has to be clamped or the number spills
        // over the skull (the 18.08 defect, found in the frame).
        float headHeight = HeadHeightMeters(head, heightMeters);
        float requested = Mathf.Max(0.005f, heightMeters);
        float target = Mathf.Min(requested, headHeight * NumberHeadHeightFraction);

        GameObject label = new GameObject("ArenaNumber");
        label.layer = humanObject.layer;
        label.transform.SetParent(head, false);
        label.transform.localPosition = new Vector3(0f, 0f, -0.1f);   // centred in the head
        label.transform.localRotation = Quaternion.identity;

        TextMesh textMesh = label.AddComponent<TextMesh>();
        Font font = LabelFont();
        if (font != null) textMesh.font = font;
        textMesh.text = number.ToString(CultureInfo.InvariantCulture);
        textMesh.fontSize = 72;
        // First guess from the identity the earlier cycle used (world height =
        // characterSize * fontSize / 10); the agent measures the drawn mesh and
        // corrects it on the next step, so the guess is never the claim.
        textMesh.characterSize = Mathf.Max(0.005f, target * 10f / textMesh.fontSize);
        textMesh.anchor = TextAnchor.MiddleCenter;
        textMesh.alignment = TextAlignment.Center;
        textMesh.color = new Color(0f, 0f, 0f, 1f);

        MeshRenderer renderer = label.GetComponent<MeshRenderer>();
        if (renderer != null)
        {
            renderer.sortingOrder = 200;
            if (font != null && font.material != null)
                renderer.sharedMaterial = font.material;
        }

        ArenaNumberTarget intent = label.AddComponent<ArenaNumberTarget>();
        intent.id = number;
        intent.headHeightMeters = headHeight;
        intent.requestedHeightMeters = requested;
        intent.targetHeightMeters = target;
        NumberSpawned++;
        return textMesh;
    }

    // The head's own collider box (world AABB). Fallback: a human head is about an
    // eighth of the body - used only when the rig has no collider on the head.
    public static float HeadHeightMeters(Transform head, float bodyHeightMeters)
    {
        Collider2D box = head != null ? head.GetComponent<Collider2D>() : null;
        if (box != null && box.bounds.size.y > 1e-4f)
            return box.bounds.size.y;
        return Mathf.Max(0.05f, bodyHeightMeters * 0.125f);
    }

    // Carries the digit's intent from AttachNumber (called by ArenaRunner before
    // the agent exists) to the agent's own first measurement - its own top-level
    // file, because Unity will not add a nested class as a component.
}
