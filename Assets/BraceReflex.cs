using System.Globalization;
using UnityEngine;

// ─── THE BRACE REFLEX (29.09, task "Tilt-Platform Stand"; owner's addendum 5, 29.09) ───
//
// Owner (verbatim, RU): "К концу этого видео было бы логично, если бы он уперся руками в
// опору, которая приближалась к его лицу. Тогда бы он лучше удержался. Необходимо добавить
// следующую логику. Если близко к голове приближается, на вытянутых руках, какая-то
// поверхность, то человек выставляет руки на нее. Это естественный, безусловный рефлекс."
//
// WHAT. A surface (the tilting deck's raised side, a wall, the ground during a fall) that
// comes inside arm's reach of the head/chest gets the hands: both arms reach for it through
// ArmDrive's command route (one writer per joint) and, once a palm touches (ArmDrive's own
// HandContactL/R), the elbow extends into a PUSH - the braced arm is an extra support point
// and the man holds better or the fall changes shape. The trigger is UNCONDITIONAL: no
// chance calculation, no margin gate - proximity of a surface plus its approach is the
// whole rule (the FallReflex's rule is the opposite one: it waits for the fall to be
// inevitable). The two compose on one body: the fall reflex keeps the step and the head,
// the brace keeps the arms while its own surface is still converging inside reach.
//
// INERT without a fire: nothing is read but the rig's published state until the trigger;
// the component is added only to -tiltPlatform takes and -braceReflex 0 removes it, so the
// disarmed control take stays byte-identical to the r6 record (round 7 report).
//
// KNOBS (all read once in Awake):
//   -braceReflex 0/1       arm it (default 1 on -tiltPlatform takes)
//   -braceReach m          probe radius around the head/chest (default 0.95)
//   -braceFireDist m       fire on proximity alone, no approach needed (default 0.30)
//   -braceApproach m/s     fire when the surface closes faster than this inside reach (default 0.35)
//   -braceMarginGate m     r8 gate: fire only while the support margin is under this (default 0.05;
//                          r7's early fires at margin +0.106/+0.125 cost hold - this bans them;
//                          a value >= 1 restores the old ungated reflex)
//   -braceDelay s          latency before the arms move (default 0.03)
//   -braceRamp s           arms ramp-in (default 0.12)
//   -braceShoulderDeg d    shoulder target, minus = forward-down (default -80)
//   -braceElbowDeg d       elbow target before contact, minus = folded (default -25)
//   -bracePushDeg d        elbow target after palm contact (default -5 = nearly straight)
//   -bracePushRamp s       the push builds over this (default 0.35)
//   -braceHold s           latch cap (default 6; 0 = never by time)
//   -braceRearm s          quiet time after a release (default 1.0)
//   -braceLog 0/1          fire/contact/release lines (default 1)
//   -braceForce s          PROBE ONLY: fire at t=s (standing) regardless of geometry
[DefaultExecutionOrder(21)]   // after FallReflex (20): while the brace owns, its pose is the last write
public class BraceReflex : MonoBehaviour
{
    // ─── knobs ───
    private bool armed = true;
    private float reach = 0.95f;
    private float fireDist = 0.30f;
    private float approachSpeed = 0.35f;
    private float marginGate = 0.05f;
    private float reflexDelay = 0.03f;
    private float armRamp = 0.12f;
    private float shoulderDeg = -80f;
    private float elbowDeg = -25f;
    private float pushDeg = -5f;
    private float pushRamp = 0.35f;
    private float holdSeconds = 6f;
    private float rearmSeconds = 1f;
    private bool logTrace = true;
    private float forceTime = -1f;
    private float maxBodyTiltDeg = 80f;   // a body already down is the get-up's business, not ours
    private const float SurfaceGoneFactor = 1.4f;
    private const float SurfaceGoneSeconds = 0.6f;
    private const float StableTiltDeg = 12f;
    private const float StableMargin = 0.04f;
    private const float StableSeconds = 0.8f;

    private BalanceController balance;
    private BodyStateEstimator state;
    private VestibularSystem vest;
    private ArmDrive arms;
    private FallReflex fall;
    private Transform headT;
    private Transform torsoT;

    // ─── latch state ───
    private bool latched;
    private bool forceDone;
    private float latency;                // seconds since fire
    private float fireTime = -1f;
    private float rearmWait;
    private float goneTimer;
    private float stableTimer;
    private float contactLTime = -1f;
    private float contactRTime = -1f;
    private bool pushed;
    private float pushAt = -1f;
    private bool armsOwned;
    private float prevDist = -1f;         // the nearest surface distance of the previous frame
    private bool suppressLogged;          // one suppress line per continuous approach episode

    // read-only state for the recorder (TiltStandLog) and the dispatcher's reports
    public bool Fired { get { return latched; } }
    public float FireTime { get { return fireTime; } }
    public int FireCount { get; private set; }
    public int SuppressedCount { get; private set; }      // approaches refused by the margin gate
    public float FirstFireTime { get; private set; }      // take-scoped, -1 until the first fire
    public int ContactFrames { get; private set; }        // frames with a palm on a surface while latched
    public float FirstContactTime { get; private set; }   // take-scoped, -1 until a palm lands
    public string LastFirePart { get; private set; }
    public float LastFireDist { get; private set; }
    public float LastFireClosing { get; private set; }

    private readonly Collider2D[] hitBuf = new Collider2D[8];
    private Vector2 probeBox;

    void Awake()
    {
        armed = GetFloat("-braceReflex", armed ? 1f : 0f) > 0.5f;
        reach = Mathf.Clamp(GetFloat("-braceReach", reach), 0.2f, 2f);
        probeBox = new Vector2(reach * 2f, reach * 2f);
        fireDist = Mathf.Clamp(GetFloat("-braceFireDist", fireDist), 0.05f, 2f);
        approachSpeed = Mathf.Clamp(GetFloat("-braceApproach", approachSpeed), 0.02f, 5f);
        marginGate = Mathf.Clamp(GetFloat("-braceMarginGate", marginGate), -1f, 2f);
        reflexDelay = Mathf.Clamp(GetFloat("-braceDelay", reflexDelay), 0f, 0.5f);
        armRamp = Mathf.Max(0.02f, GetFloat("-braceRamp", armRamp));
        shoulderDeg = Mathf.Clamp(GetFloat("-braceShoulderDeg", shoulderDeg), -90f, 90f);
        elbowDeg = Mathf.Clamp(GetFloat("-braceElbowDeg", elbowDeg), -135f, -5f);
        pushDeg = Mathf.Clamp(GetFloat("-bracePushDeg", pushDeg), -135f, -5f);
        pushRamp = Mathf.Max(0.05f, GetFloat("-bracePushRamp", pushRamp));
        holdSeconds = Mathf.Max(0f, GetFloat("-braceHold", holdSeconds));
        rearmSeconds = Mathf.Max(0f, GetFloat("-braceRearm", rearmSeconds));
        logTrace = GetFloat("-braceLog", logTrace ? 1f : 0f) > 0.5f;
        if (HasArg("-braceForce")) forceTime = Mathf.Max(0f, GetFloat("-braceForce", -1f));
        FirstContactTime = -1f;
        FirstFireTime = -1f;
        LastFirePart = "none";
    }

    void Start()
    {
        balance = GetComponent<BalanceController>();
        state = GetComponent<BodyStateEstimator>();
        vest = GetComponent<VestibularSystem>();
        arms = GetComponent<ArmDrive>();
        fall = GetComponent<FallReflex>();
        headT = transform.Find("Head");
        torsoT = transform.Find("Torso");
        if (balance == null || state == null || arms == null)
        {
            Debug.LogWarning("BRACEREFLEX: no BalanceController/BodyStateEstimator/ArmDrive on " + name + " - inert");
            enabled = false;
            return;
        }
        if (!armed)
        {
            enabled = false;
            return;
        }
        Debug.Log("BRACEREFLEX armed reach=" + F(reach) + " fire=" + F(fireDist) + " approach=" + F(approachSpeed)
            + " arm=" + F(shoulderDeg) + "/" + F(elbowDeg) + " push=" + F(pushDeg)
            + " gate=" + F(marginGate));
    }

    private static string F(float v)
    {
        return v.ToString("0.###", CultureInfo.InvariantCulture);
    }

    void FixedUpdate()
    {
        float dt = Time.fixedDeltaTime;

        if (!latched)
        {
            if (rearmWait > 0f)
                rearmWait -= dt;

            // ── fire test: a surface inside reach of the head/chest, approached ──
            string part; float dist; float closing;
            bool sensed = Sense(out part, out dist, out closing);
            if (forceTime >= 0f && !forceDone && Time.time >= forceTime
                && balance.locomotionState == BalanceController.LocomotionState.Standing)
            {
                forceDone = true;
                Fire("probe", 0f, 0f);
                return;
            }
            if (!sensed || rearmWait > 0f)
                return;
            bool legal = (balance.locomotionState == BalanceController.LocomotionState.Standing
                    || balance.locomotionState == BalanceController.LocomotionState.Falling)
                && BodyTilt() < maxBodyTiltDeg;
            if (!legal)
                return;
            bool approach = dist < fireDist || (dist < reach && closing > approachSpeed);
            if (!approach)
            {
                suppressLogged = false;
                return;
            }
            // THE r8 GATE: an approach alone is not a brace moment - the balance must actually be
            // degrading. r7's two early fires (margin +0.106 no-contact, +0.125 -> -4 deg hold)
            // burned angle; no fire may happen while the margin is healthy. A fired FallReflex
            // also unlocks the brace (the fall is on, whatever the margin says this frame).
            bool threat = state.supportMargin < marginGate || (fall != null && fall.Fired);
            if (threat)
            {
                Fire(part, dist, closing);
                return;
            }
            if (!suppressLogged)
            {
                suppressLogged = true;
                SuppressedCount++;
                if (logTrace)
                    Debug.Log("BRACEREFLEX suppressed t=" + F(Time.time) + " part=" + part
                        + " d=" + F(dist) + " closing=" + F(closing)
                        + " margin=" + F(state.supportMargin) + " gate=" + F(marginGate));
            }
            return;
        }

        // ── latched ──
        latency += dt;

        // ── release tests ──
        if (BodyTilt() > maxBodyTiltDeg + 5f)
        {
            Release("down");
            return;
        }
        if (holdSeconds > 0f && latency >= holdSeconds)
        {
            Release("hold");
            return;
        }
        string part2; float dist2; float closing2;
        bool sensedNow = Sense(out part2, out dist2, out closing2);

        // The FallReflex outranks the brace only once the brace's own surface is gone:
        // while a surface keeps converging inside the brace's reach the arms are the
        // brace's job - the reach and the push the owner asked for - and the fall reflex
        // keeps the step and the head. A yield must not clear the command; the fall
        // reflex's own pose already sits there this frame.
        if (fall != null && fall.Fired && (!sensedNow || dist2 > reach * SurfaceGoneFactor))
        {
            Release("fallreflex");
            return;
        }
        if (!sensedNow || dist2 > reach * SurfaceGoneFactor)
            goneTimer += dt;
        else
            goneTimer = 0f;
        bool standingClean = balance.locomotionState == BalanceController.LocomotionState.Standing
            && state.leftFootGrounded && state.rightFootGrounded
            && state.supportMargin > StableMargin && BodyTilt() < StableTiltDeg;
        stableTimer = standingClean ? stableTimer + dt : 0f;
        if (goneTimer >= SurfaceGoneSeconds && state.supportMargin > StableMargin)
        {
            Release("surface-gone");
            return;
        }
        if (latency >= 1f && stableTimer >= StableSeconds)
        {
            Release("stable");
            return;
        }

        // ── the sequence: reach, then push ──
        bool contactL = arms.HandContactL;
        bool contactR = arms.HandContactR;
        if (contactL || contactR)
        {
            ContactFrames++;
            if (contactL && contactLTime < 0f)
            {
                contactLTime = Time.time;
                if (FirstContactTime < 0f) FirstContactTime = Time.time;
                if (logTrace)
                    Debug.Log("BRACEREFLEX hand L contact t=" + F(Time.time) + " d=" + F(dist2));
            }
            if (contactR && contactRTime < 0f)
            {
                contactRTime = Time.time;
                if (FirstContactTime < 0f) FirstContactTime = Time.time;
                if (logTrace)
                    Debug.Log("BRACEREFLEX hand R contact t=" + F(Time.time) + " d=" + F(dist2));
            }
            if (!pushed)
            {
                pushed = true;
                pushAt = latency;
                if (logTrace)
                    Debug.Log("BRACEREFLEX push t=" + F(Time.time) + " (palm on the surface)");
            }
        }

        if (armsOwned)
        {
            float armDose = Mathf.Clamp01((latency - reflexDelay) / armRamp);
            float elbowTarget = elbowDeg;
            if (pushed)
            {
                float pushDose = Mathf.Clamp01((latency - pushAt) / pushRamp);
                elbowTarget = Mathf.Lerp(elbowDeg, pushDeg, pushDose);
            }
            arms.reflexShoulderDeg = shoulderDeg * armDose;
            arms.reflexElbowDeg = elbowTarget * armDose;
        }
    }

    // The nearest surface inside `reach` of the head or the chest, its distance, and how
    // fast it closes (m/s). Closing is the larger of the body part's own motion toward the
    // surface and the frame-over-frame shrink of the distance (the deck's own rise).
    private bool Sense(out string part, out float bestDist, out float closing)
    {
        part = "none";
        bestDist = float.MaxValue;
        closing = 0f;
        int bestIndex = -1;
        Vector2 bestPoint = Vector2.zero;
        Vector2 bestVel = Vector2.zero;
        for (int p = 0; p < 2; p++)
        {
            if (ProbeMissing(p))
                continue;
            Vector2 point = ProbePoint(p);
            Vector2 vel = ProbeVelocity(p);
            int n = Physics2D.OverlapBoxNonAlloc(point, probeBox, 0f, hitBuf, GroundLayers.Mask);
            for (int i = 0; i < n; i++)
            {
                Collider2D col = hitBuf[i];
                if (col == null || !col.enabled || col.isTrigger)
                    continue;
                Vector2 closest = col.ClosestPoint(point);
                float d = Vector2.Distance(point, closest);
                if (d < bestDist)
                {
                    bestDist = d;
                    bestPoint = closest;
                    bestVel = vel;
                    bestIndex = p;
                }
            }
        }
        if (bestIndex < 0)
        {
            prevDist = -1f;
            return false;
        }
        part = bestIndex == 0 ? "head" : "chest";
        Vector2 from = ProbePoint(bestIndex);
        Vector2 dir = bestPoint - from;
        float motionClosing = dir.sqrMagnitude > 1e-8f
            ? Vector2.Dot(bestVel, dir.normalized)
            : 0f;
        float trackClosing = (prevDist >= 0f && Time.fixedDeltaTime > 0f)
            ? (prevDist - bestDist) / Time.fixedDeltaTime
            : 0f;
        closing = Mathf.Max(motionClosing, trackClosing);
        prevDist = bestDist;
        return true;
    }

    private bool ProbeMissing(int index)
    {
        return index == 0 ? headT == null : torsoT == null;
    }

    private Vector2 ProbePoint(int index)
    {
        Transform t = index == 0 ? headT : torsoT;
        return t != null ? (Vector2)t.position : Vector2.zero;
    }

    private Vector2 ProbeVelocity(int index)
    {
        Transform t = index == 0 ? headT : torsoT;
        if (t != null)
        {
            Rigidbody2D rb = t.GetComponent<Rigidbody2D>();
            if (rb != null)
                return rb.linearVelocity;
        }
        Rigidbody2D own = GetComponent<Rigidbody2D>();
        return own != null ? own.linearVelocity : Vector2.zero;
    }

    private float BodyTilt()
    {
        return vest != null ? Mathf.Abs(vest.GetBodyTilt()) : 0f;
    }

    private void Fire(string part, float dist, float closing)
    {
        latched = true;
        latency = 0f;
        goneTimer = 0f;
        stableTimer = 0f;
        fireTime = Time.time;
        if (FirstFireTime < 0f) FirstFireTime = Time.time;
        FireCount++;
        suppressLogged = false;
        armsOwned = true;
        LastFirePart = part;
        LastFireDist = dist;
        LastFireClosing = closing;
        if (logTrace)
            Debug.Log("BRACEREFLEX fire t=" + F(Time.time) + " part=" + part + " d=" + F(dist)
                + " closing=" + F(closing) + " margin=" + F(state.supportMargin)
                + " tilt=" + F(BodyTilt()));
    }

    private void Release(string reason)
    {
        latched = false;
        rearmWait = rearmSeconds;
        // Clear the command only when nobody else holds it: a fired fall reflex owns the
        // arm command itself, and a NaN here would wipe its pose for one frame.
        bool fallOwns = fall != null && fall.ArmsOwned;
        if (armsOwned && arms != null && !fallOwns)
        {
            arms.reflexShoulderDeg = float.NaN;
            arms.reflexElbowDeg = float.NaN;
        }
        armsOwned = false;
        if (logTrace)
            Debug.Log("BRACEREFLEX release t=" + F(Time.time) + " reason=" + reason
                + " age=" + F(latency));
    }

    private static bool HasArg(string name)
    {
        string[] args = System.Environment.GetCommandLineArgs();
        return System.Array.IndexOf(args, name) >= 0;
    }

    private static float GetFloat(string name, float fallback)
    {
        string[] args = System.Environment.GetCommandLineArgs();
        int index = System.Array.IndexOf(args, name);
        if (index < 0 || index + 1 >= args.Length) return fallback;
        return float.TryParse(args[index + 1], NumberStyles.Float,
            CultureInfo.InvariantCulture, out float value) ? value : fallback;
    }
}
