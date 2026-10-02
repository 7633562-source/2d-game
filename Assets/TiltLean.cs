using System.Globalization;
using UnityEngine;

// ─── THE TILT LEAN (29.09 round 6, task "Tilt-Platform Stand") ───
//
// The owner's program (28.09, verbatim): "он должен АКТИВНО держать свой баланс - менять
// расстояние между ногами, прогибаться вперёд или назад, сгибать ноги или руки". The stance
// channel was priced in round 5 (a context lever - the widen steps cost 2-4 deg on the
// rigid rocker and the width does not persist). This component is the POSTURE half of the
// answer: while the deck rocks, the man rides it the way a human rides a tilting surface -
// he folds his knees a little (lower CoM, more time) and leans his trunk AWAY from the
// dropping end, toward the rising one (the compensatory lean the round-2 eye review asked
// for, "нет компенсаторного наклона корпуса").
//
// WHERE THE FALL COMES FROM (measured, by column name): on the +angle side the deck drops
// under the FRONT foot (the man faces -X; the +X end rises - TiltPlatform.MoveRotation is
// CCW). The left foot stays loaded while the right foot floats (r5 control: footLoadedL
// 1.00 / footLoadedR 0.00 through the +26 hold; soleClearR +0.075), the CoM drifts forward
// (comX +0.06 -> -0.51 over 243.0-244.3) and the margin collapses at 243.76. The measured
// fall runs toward -X (r2/r4 records), i.e. FORWARD on the + side. So the ride at +angle
// is a BACKWARD lean (the rising end is +X), and at -angle a forward one: lean = -sign(angle).
//
// BOTH CHANNELS RIDE THE RIG'S OWN MACHINERY - no tug of war with the balance core:
//   KNEES - MotionIntent.crouch (the same field the editor's S key and the trial's -crouch
//           write). The core's own crouch regulator moves the pose at its own rate and its
//           own load shape; the arms take the crouch's balance pose on their own.
//   TRUNK - MotionIntent.lean (+1 forward, -1 back; BalanceController header "Trunk lean").
//           The core's cycle-82 lean machinery (leanTorsoAngle 12 / leanPelvisTilt 8 /
//           leanComShift 0.012, the D5 shape) answers with chest, pelvis and a real CoM
//           shift of the support loop.
//
// WHO WRITES THE INTENT (round-6 lesson, measured): the trial's own FixedUpdate
// (UpdateCrouchIntent/UpdateLeanIntent) and the editor's PlayerInputSource.Update re-write
// both fields EVERY step, and the balance core reads them inside its own FixedUpdate - so
// a writer at any other execution order is invisible (round 6 battery A: armed takes came
// out bit-identical to the control). This component therefore only COMPUTES the doses
// (CrouchDose / LeanDose, one step ahead) and the path that owns the intent write adds
// them: HeadlessTrial's two update methods in batch, and this component's LateUpdate in
// the editor when ownIntent is set (GameProcess). One writer per field on every path.
//
// GATE. Only while a deck is assigned AND the body stands on it (Standing, and the fall
// reflex has not fired - once it fires the arms and the step leg are the reflex's show).
// Everything else releases the doses to zero at releasePerSecond, so a fall runs exactly
// its own machinery. Disarmed (-tiltLean 0 in the trial) the component is not even added:
// a take without it is provably bit for bit (the round's control fence).
//
// KNOBS (read once in Awake):
//   -tiltLean 0/1   arm the layer (the trial adds it only for -tiltPlatform takes)
//   -tlDead deg     dead zone: |deck angle| where the ride starts (default 8)
//   -tlFull deg     |deck angle| where the ride saturates (default 30)
//   -tlKnee lvl     crouch dose at full tilt, 0..1 (default 0.15)
//   -tlLean lvl     |intent.lean| dose at full tilt, 0..1 (default 0.7)
//   -tlMarMargin m  ROUND 19 (the balance-refs integration): the support margin where the
//                   margin-lean starts growing (default 0 = off). Below it the lean gains
//                   a dose linear in the deficit (m0 - margin)/m0, capped at m0 <= 0.
//   -tlMarGain lvl  ROUND 19: extra |intent.lean| dose at a zero margin, 0..1 (default 0
//                   = off). Either knob at 0 kills the rung; the side comes from the
//                   estimator's own geometry (push AWAY from the near edge), so a drift
//                   either way is corrected, not only the angle-sign side.
//   -tlRate /s      dose slew, per second (default 1)
//   -tlRelease /s   dose release slew when not riding (default 2)
//   -tlLog 0/1      one line on dose changes / release (default 1)
[DefaultExecutionOrder(25)]
public class TiltLean : MonoBehaviour
{
    // ─── knobs ───
    private float deadDeg = 8f;
    private float fullDeg = 30f;
    private float kneeMax = 0.15f;
    private float leanMax = 0.7f;
    private float kFloor;                 // the crouch never releases below this while the ramp is live
    private float ratePerSecond = 1f;
    private float releasePerSecond = 2f;
    private bool logTrace = true;
    private float marginM0;               // round 19: margin where the margin-lean starts (0 = off)
    private float marginGain;             // round 19: margin-lean dose at a zero margin (0 = off)

    public TiltPlatform platform;
    // The editor path (GameProcess) sets this: no trial exists there, so the component
    // writes MotionIntent itself (LateUpdate - the last phase before the core's next read).
    public bool ownIntent;

    private BalanceController balance;
    private BodyStateEstimator state;
    private MotionIntent intent;
    private FallReflex reflex;

    private float crouchDose;
    private float leanDose;
    private float marginDose;
    private float lastLogCrouch = -9f;
    private float lastLogLean = -9f;
    private float lastLogMargin = -9f;
    private bool wasRiding;
    private const float LogStep = 0.06f;

    // The doses the intent owner adds (trial) / this component writes (editor).
    public float CrouchDose { get { return crouchDose; } }
    public float LeanDose { get { return Mathf.Clamp(leanDose + marginDose, -1f, 1f); } }

    void Awake()
    {
        deadDeg = Mathf.Clamp(GetFloat("-tlDead", deadDeg), 0f, 45f);
        fullDeg = Mathf.Clamp(GetFloat("-tlFull", fullDeg), deadDeg + 1f, 60f);
        kneeMax = Mathf.Clamp(GetFloat("-tlKnee", kneeMax), 0f, 0.5f);
        leanMax = Mathf.Clamp(GetFloat("-tlLean", leanMax), 0f, 1f);
        marginM0 = Mathf.Max(0f, GetFloat("-tlMarMargin", marginM0));
        marginGain = Mathf.Max(0f, GetFloat("-tlMarGain", marginGain));
        kFloor = Mathf.Clamp(GetFloat("-tlFloor", kFloor), 0f, 0.5f);
        ratePerSecond = Mathf.Max(0.05f, GetFloat("-tlRate", ratePerSecond));
        releasePerSecond = Mathf.Max(0.05f, GetFloat("-tlRelease", releasePerSecond));
        logTrace = GetFloat("-tlLog", logTrace ? 1f : 0f) > 0.5f;
    }

    void Start()
    {
        balance = GetComponent<BalanceController>();
        state = GetComponent<BodyStateEstimator>();
        intent = GetComponent<MotionIntent>();
        reflex = GetComponent<FallReflex>();
        if (balance == null || state == null || intent == null)
        {
            Debug.LogWarning("TILTLEAN: no BalanceController/BodyStateEstimator/MotionIntent on "
                + name + " - inert");
            enabled = false;
            return;
        }
        if (platform == null)
        {
            Debug.LogWarning("TILTLEAN: no deck assigned on " + name + " - inert");
            enabled = false;
            return;
        }
        Debug.Log("TILTLEAN armed dead=" + F(deadDeg) + " full=" + F(fullDeg)
            + " knee=" + F(kneeMax) + " lean=" + F(leanMax) + " floor=" + F(kFloor)
            + " marM0=" + F(marginM0) + " marGain=" + F(marginGain)
            + " writer=" + (ownIntent ? "self" : "trial"));
    }

    private static string F(float v)
    {
        return v.ToString("0.###", CultureInfo.InvariantCulture);
    }

    // Compute only - the intent write belongs to the path that owns it (see the header).
    void FixedUpdate()
    {
        float dt = Time.fixedDeltaTime;
        bool riding = balance.locomotionState == BalanceController.LocomotionState.Standing
            && (reflex == null || !reflex.Fired);

        float crouchTarget = 0f;
        float leanTarget = 0f;
        float marginTarget = 0f;
        float angle = platform.CurrentAngleDeg;
        if (riding)
        {
            float k = Mathf.SmoothStep(0f, 1f,
                Mathf.InverseLerp(deadDeg, fullDeg, Mathf.Abs(angle)));
            // The floor: once the ramp is live the crouch does not drop to zero inside the
            // dead zone - the cross-zero MOVE of a big step is the violent part of the
            // cycle (the r6 crashes at 32-36 all arrive on it) and a full release there is
            // an artifact, not a posture. Crouch only; the lean keeps its zero at level.
            float kc = platform.RampTime > platform.settleSeconds ? Mathf.Max(kFloor, k) : k;
            crouchTarget = kneeMax * kc;
            // +angle: the front side drops, the CoM drifts -X; the rising end is +X, so the
            // ride leans back (intent.lean -1 = back). The - side mirrors it.
            leanTarget = -leanMax * k * (angle >= 0f ? 1f : -1f);

            // THE MARGIN RUNG (round 19, the balance-refs integration): the lean gain
            // rises as the support margin shrinks. Fire only under marginM0; push AWAY
            // from the NEAR edge - the estimator's own geometry decides the side, so a
            // drift either way is covered. Both knobs default 0 -> the rung is inert.
            if (marginM0 > 0f && marginGain > 0f)
            {
                float mo = state.supportMargin;
                float deficit = Mathf.Clamp01((marginM0 - mo) / marginM0);
                float dir = state.comPosition.x <= state.supportCenterX ? -1f : 1f;
                marginTarget = dir * marginGain * deficit;
            }
        }

        float rate = riding ? ratePerSecond : releasePerSecond;
        crouchDose = Mathf.MoveTowards(crouchDose, crouchTarget, rate * dt);
        leanDose = Mathf.MoveTowards(leanDose, leanTarget, rate * dt);
        marginDose = Mathf.MoveTowards(marginDose, marginTarget, rate * dt);

        if (logTrace)
        {
            if (wasRiding && !riding)
                Debug.Log("TILTLEAN release t=" + F(Time.time)
                    + " crouch=" + F(crouchDose) + " lean=" + F(leanDose)
                    + " mar=" + F(marginDose));
            else if (riding
                && (Mathf.Abs(crouchDose - lastLogCrouch) >= LogStep
                    || Mathf.Abs(leanDose - lastLogLean) >= LogStep
                    || Mathf.Abs(marginDose - lastLogMargin) >= LogStep))
            {
                Debug.Log("TILTLEAN t=" + F(Time.time) + " angle=" + F(angle)
                    + " crouch=" + F(crouchDose) + " lean=" + F(leanDose)
                    + " margin=" + F(state.supportMargin) + " mar=" + F(marginDose));
                lastLogCrouch = crouchDose;
                lastLogLean = leanDose;
                lastLogMargin = marginDose;
            }
        }
        wasRiding = riding;
    }

    // The editor writer: after PlayerInputSource.Update, before the core's next read.
    void LateUpdate()
    {
        if (!ownIntent || intent == null) return;
        intent.crouch = Mathf.Clamp01(intent.crouch + crouchDose);
        intent.lean = Mathf.Clamp(intent.lean + LeanDose, -1f, 1f);
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
