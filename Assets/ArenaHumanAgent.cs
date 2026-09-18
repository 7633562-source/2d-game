using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

// ARENA-1: one arena human. Owns nothing but the measurement - the body itself is
// the standard rig (Human.BuildHuman), so the numbers stay comparable with the
// walk trials.
//
// Upright / fallen come from the PHYSICAL criterion of the card, not from the
// trial `fell` flag (that flag is known to lie and is never read here):
//   fallen = |torso tilt| > 60 deg  OR  pelvis more than 0.25 m below THIS
//            human's own standing value, sustained 0.1 s
// The standing value is the running maximum of the pelvis height while the body is
// still upright, i.e. the same trick PoseDriveController uses for its stand frame,
// taken per human so bodies of different ages cannot poison each other.
//
// The trajectory ring (every 0.25 s, first 15 s) exists for one reason: the elite
// genome is re-spawned unchanged, and the two copies must walk the same distance
// and the same early path. If a younger copy is deflected, a human-human collision
// is still live.
public class ArenaHumanAgent : MonoBehaviour
{
    public const float FallTiltDegrees = 60f;
    public const float FallDropMeters = 0.25f;
    public const float FallSustainSeconds = 0.1f;
    private const int TrajectorySlots = 64;
    private const float TrajectoryPeriod = 0.25f;

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
    private float standPelvisY = float.NegativeInfinity;
    private float downAge;
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

        lastTilt = Mathf.Abs(vestibular != null ? vestibular.GetBodyTilt() : 0f);
        lastPelvisY = pelvis.position.y;

        if (!fallen)
        {
            // The stand reference only climbs while the body is upright: a body
            // that has already gone down must not raise its own bar.
            if (lastTilt <= FallTiltDegrees)
                standPelvisY = Mathf.Max(standPelvisY, lastPelvisY);

            bool downNow = lastTilt > FallTiltDegrees
                || lastPelvisY < standPelvisY - FallDropMeters;
            if (downNow)
            {
                downAge += simDelta;
                if (downAge >= FallSustainSeconds)
                {
                    fallen = true;
                    fallTime = simTime;
                    tiltAtFall = lastTilt;
                }
            }
            else
            {
                downAge = 0f;
            }
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

    public bool UprightNow()
    {
        return !fallen
            && lastTilt <= FallTiltDegrees
            && (float.IsInfinity(standPelvisY) || lastPelvisY >= standPelvisY - FallDropMeters);
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

        GameObject label = new GameObject("ArenaNumber");
        label.layer = humanObject.layer;
        label.transform.SetParent(head, false);
        label.transform.localPosition = new Vector3(0f, 0.16f, -0.1f);
        label.transform.localRotation = Quaternion.identity;

        TextMesh textMesh = label.AddComponent<TextMesh>();
        Font font = LabelFont();
        if (font != null) textMesh.font = font;
        textMesh.text = number.ToString(CultureInfo.InvariantCulture);
        textMesh.fontSize = 72;
        textMesh.characterSize = Mathf.Max(0.005f, heightMeters) / 7.2f;
        textMesh.anchor = TextAnchor.LowerCenter;
        textMesh.alignment = TextAlignment.Center;
        textMesh.color = new Color(0f, 0f, 0f, 1f);

        MeshRenderer renderer = label.GetComponent<MeshRenderer>();
        if (renderer != null)
        {
            renderer.sortingOrder = 200;
            if (font != null && font.material != null)
                renderer.sharedMaterial = font.material;
        }
        return textMesh;
    }
}
