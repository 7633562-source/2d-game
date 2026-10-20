using UnityEngine;

// ─── THE SELF-ORIENTATION CORE (29.09 round 17, the owner's addendum 8) ───
// "нам нужно создать логику анализа своего положения человека относительно
// окружающего мира, это основа нашей физики человека". One state, computed at
// 200 Hz from the torso's own world rotation - the rig is 2D, so the sagittal
// plane carries the whole story: standing / on-back / on-belly / side (the roll
// quadrants past the upright cone). Every reflex and layer that reaches for a
// surface reads LyingBack and branches by it: a supine body must never be driven
// into the face-down reach (the owner's evidence frame: a man on his back, arms
// driven "forward" - which in the world points them straight off the deck).
//
// The component exists only when a take armed it (-poseOrient 1; the attach
// sites are HeadlessTrial.BuildTrialScene and GameProcess), so every other take
// stays bit for bit. Signals: the torso rigidbody's world rotation (the same
// read VestibularSystem.GetBodyTilt uses) and its roll rate; nothing else is
// needed for the v0 bands. Kneeling is an overlay (upright torso + a dropped
// pelvis) - logged, not yet branched on.
[DefaultExecutionOrder(-90)]
public class PoseOrientation : MonoBehaviour
{
    public enum Pose
    {
        Upright = 0,
        Supine = 1,    // on the back: the backward hemisphere past the cone
        Prone = 2,     // on the belly: the forward hemisphere past the cone
        Side = 3,      // rolled past the near-inverted line on either side
        Unknown = 4,   // no torso resolved
    }

    // ─── knobs ───
    // The arm rest pose the supine branch drives: arms along the body (the
    // owner's "to the sides"), the elbow keeps a human bend. shoulder 0 = the
    // arm hanging in the torso frame (the standing rest ArmDrive uses).
    public float sideShoulderDeg = -6f;
    public float sideElbowDeg = -50f;
    // The head target while supine: a CHIN TUCK (positive bias). The face-fall's
    // tip-back (-8, the r4 default) would meet a back fall skull-first; the r4
    // head-sign probe pinned the two signs (bias +25 -> head -5..-28 = tucked).
    public float supineHeadBiasDeg = 20f;
    // The cone a pose must leave to be called lying, deg from vertical. 45 deg:
    // outside it no ankle strategy holds a standing body anyway.
    public float uprightConeDeg = 45f;
    public float rollRateDegPerSec = 120f;
    public bool logTrace = true;

    // ─── state (public for logging) ───
    public Pose State { get; private set; }
    public float TorsoDeg { get; private set; }    // torso world rotation, deg (Unity CCW+)
    public float FacingY { get; private set; }     // face direction: +1 face-up .. -1 face-down
    public bool LyingBack { get; private set; }    // the backward hemisphere past the cone
    public bool Rolling { get; private set; }
    public bool Kneeling { get; private set; }
    public bool Valid { get { return torsoRb != null; } }
    public string StateName { get { return State.ToString(); } }

    private Rigidbody2D torsoRb;
    private Rigidbody2D pelvisRb;
    private float standPelvisY;
    private bool pelvisCaptured;
    private Pose lastLogged = Pose.Unknown;

    void Awake()
    {
        Transform torso = transform.Find("Torso");
        if (torso == null) torso = transform.Find("Chest");
        torsoRb = torso != null ? torso.GetComponent<Rigidbody2D>() : null;
        Transform pelvis = transform.Find("Pelvis");
        pelvisRb = pelvis != null ? pelvis.GetComponent<Rigidbody2D>() : null;

        sideShoulderDeg = Mathf.Clamp(GetFloat("-poseSideShoulder", sideShoulderDeg), -85f, 85f);
        sideElbowDeg = Mathf.Clamp(GetFloat("-poseSideElbow", sideElbowDeg), -135f, -5f);
        supineHeadBiasDeg = Mathf.Clamp(GetFloat("-poseHeadTuck", supineHeadBiasDeg), -30f, 30f);
        uprightConeDeg = Mathf.Clamp(GetFloat("-poseUprightCone", uprightConeDeg), 15f, 80f);
        rollRateDegPerSec = Mathf.Max(10f, GetFloat("-poseRollRate", rollRateDegPerSec));
        logTrace = GetFloat("-poseLog", logTrace ? 1f : 0f) > 0.5f;

        if (torsoRb == null)
            Debug.LogWarning("POSEORIENT: no Torso/Chest on " + name + " - inactive");
        else if (logTrace)
            Debug.Log("POSEORIENT armed cone=" + F(uprightConeDeg)
                + " side=" + F(sideShoulderDeg) + "/" + F(sideElbowDeg)
                + " headTuck=" + F(supineHeadBiasDeg));
    }

    void FixedUpdate()
    {
        if (torsoRb == null)
        {
            State = Pose.Unknown;
            LyingBack = false;
            Rolling = false;
            Kneeling = false;
            return;
        }

        float deg = Mathf.DeltaAngle(0f, torsoRb.rotation);   // (-180, 180]
        TorsoDeg = deg;
        FacingY = Mathf.Sin(deg * Mathf.Deg2Rad);
        Rolling = Mathf.Abs(torsoRb.angularVelocity) >= rollRateDegPerSec;

        float cone = uprightConeDeg;
        float nearInverted = 180f - cone;
        if (Mathf.Abs(deg) <= cone) State = Pose.Upright;
        else if (deg > cone) State = deg <= nearInverted ? Pose.Supine : Pose.Side;
        else State = deg >= -nearInverted ? Pose.Prone : Pose.Side;

        LyingBack = deg > cone;

        if (pelvisRb != null)
        {
            if (!pelvisCaptured)
            {
                // The spawn pose is standing: the pelvis line of the quiet stance.
                pelvisCaptured = true;
                standPelvisY = pelvisRb.position.y;
            }
            Kneeling = State == Pose.Upright && pelvisRb.position.y < standPelvisY - 0.35f;
        }

        if (logTrace && State != lastLogged)
        {
            lastLogged = State;
            Debug.Log("ORIENT t=" + F(Time.time) + " state=" + State
                + " torso=" + F(TorsoDeg) + " face=" + F(FacingY)
                + " rolling=" + (Rolling ? 1 : 0) + " back=" + (LyingBack ? 1 : 0));
        }
    }

    private static float GetFloat(string name, float fallback)
    {
        string[] args = LauncherArgs.Args();
        int index = System.Array.IndexOf(args, ArgsAudit.Note(name));
        if (index < 0 || index + 1 >= args.Length) return fallback;
        return float.TryParse(args[index + 1], System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out float value) ? value : fallback;
    }

    private static string F(float v)
    {
        return v.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
    }
}
