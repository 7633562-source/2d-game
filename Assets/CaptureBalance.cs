using UnityEngine;

// Capture-walk trunk. Sole owner of the pelvis hold and the lumbar PD
// that keep the chest on its walking lean. Inputs are measured tilts and
// the gait's hold/restore choice; output is the lumbar world target and the
// hip joint angles that hold each thigh where it already is. Does not read
// walkPhase, xi, or muscles: FallRecovery and the swing IK stay in the
// parent, and ActuatorMixer turns these targets into activations.
//
// Lumbar. Forward is minus on a world target because the human faces +X.
// During the entry ramp the target fades from the quiet pose into the
// walking lean. Every phase tracks that lean: one-way hold in Swing
// rode a dump past it to −90° (capwalk4). Stance hip restores the same
// pelvis lean, so this is not two controllers fighting a reach IK. The
// signal formula is the existing lumbar loop (−P*error − (−D)*speed),
// not a new damper and not a sign flip.
//
// Pelvis. Every loaded hip restores the walking world lean
// (joint target = pelvisTarget − measured thigh). Tracking the
// measured tilt is zero P error: the swing-hip reaction then
// sits the trunk (capwalk3) or rocks it +8→+21° in 0.1 s after
// the first plant while the trail HOLD sleeps (capwalk14). The
// TRAIL knee still HOLDS so a trailing length-servo cannot peel.
// The parent still applies the loaded-hip torque cap.
public sealed class CaptureBalance
{
    public struct Pose
    {
        public float lumbarBaseTilt;
        public float trunkLeanDeg;
        public float dir;
        public float entryBlend;
        public bool holdTrunk;
        public bool hasTilt;
        public float measuredTilt;
    }

    public struct Gains
    {
        public float lumbarP;
        public float lumbarD;
        public float lumbarErrorRef;
        public float speedRef;
    }

    public struct Command
    {
        public float lumbarTarget;
        public float lumbarSignal;
    }

    // Hold never. One-way hold in Swing kept a forward dump: once the
    // chest passed the walk lean, Min(target, measured) rode it to −90°
    // (capwalk4). Dual support already tracked the lean so P could catch.
    // Stance hip now restores the same walking pelvis, so lumbar tracking
    // that lean is not a second controller fighting a reach IK.
    public static bool HoldTrunk(CaptureWalkState state, float entryBlend)
    {
        _ = state;
        _ = entryBlend;
        return false;
    }

    public Command Evaluate(in Pose pose, in Gains gains, float angVel)
    {
        float lumbarTarget = TrunkTarget(pose);
        float lumbarSignal = 0f;
        if (pose.hasTilt)
            lumbarSignal = LumbarSignal(pose.measuredTilt, lumbarTarget, angVel, gains);
        return new Command
        {
            lumbarTarget = lumbarTarget,
            lumbarSignal = lumbarSignal
        };
    }

    public float TrunkTarget(in Pose pose)
    {
        float torsoTarget = Mathf.Lerp(pose.lumbarBaseTilt,
            -Mathf.Abs(pose.trunkLeanDeg) * pose.dir, pose.entryBlend);
        if (pose.holdTrunk && pose.hasTilt)
        {
            torsoTarget = pose.dir >= 0f
                ? Mathf.Min(torsoTarget, pose.measuredTilt)
                : Mathf.Max(torsoTarget, pose.measuredTilt);
        }
        return torsoTarget;
    }

    // Same algebra as BalanceController.ControlLumbarToWorldUpright.
    // Gains are plus, so in P*e − D*ω they enter with a minus: that is
    // exactly the old P=−2 and D=−0.3, not a new damper.
    public static float LumbarSignal(float tilt, float targetTilt, float angVel,
                                     in Gains gains)
    {
        float error = Mathf.DeltaAngle(tilt, targetTilt)
                      / Mathf.Max(1f, gains.lumbarErrorRef);
        float speed = angVel / Mathf.Max(1f, gains.speedRef);
        return Mathf.Clamp((-gains.lumbarP) * error - (-gains.lumbarD) * speed,
            -1f, 1f);
    }

    // jointAngle = parent − child, so the hip angle that puts the thigh
    // at a world tilt is pelvisTilt − thighWorld. Holding the measured
    // thigh is a zero-error SPD: the pelvis is not restored to a lean.
    public static float HipHoldTarget(float pelvisTilt, float thighWorldDeg)
    {
        return pelvisTilt - thighWorldDeg;
    }
}
