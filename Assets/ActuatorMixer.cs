using UnityEngine;

// Capture-walk muscle write. Sole owner of turning a balance or swing
// target into Muscle.activation on this physics step. The parent still
// chooses the target (CaptureBalance for the trunk, swing IK for the
// airborne leg, CoP for the stance ankle). This class does not read
// walkPhase, xi, or FallRecovery: it never decides who owns a hinge.
//
// One convention: a positive signal means "increase jointAngle", and that
// is extensor work. ApplySignal and the SPD denominator are the same algebra
// BalanceController already uses, including the activation slew and the
// paired-inertia damper. Snap-to-zero on freeze writes activation = 0
// directly — that is the 50 ms lag kill, not a MoveTowards.
public sealed class ActuatorMixer
{
    public float activationSpeed = 20f;
    public float speedReference = 200f;
    // WALK-1 recorder completeness: the pre-clamp demand the last JointStable
    // call computed, in units of the acting muscle's ceiling (|value| > 1 means
    // the owner asked for more torque than the muscle can deliver). Recorder
    // only: it is written, never read, by the control math.
    public float lastDemand;

    public struct HipCap
    {
        public float heelNm;
        public float toeNm;
        public float pGain;
        public float errorRef;
    }

    // One convention for every joint: a positive signal means
    // "increase the joint angle", and that is extensor work.
    public void ApplySignal(float signal, Muscle flexor, Muscle extensor)
    {
        if (signal > 0f)
        {
            UpdateMuscle(extensor, signal);
            UpdateMuscle(flexor, 0f);
        }
        else
        {
            UpdateMuscle(extensor, 0f);
            UpdateMuscle(flexor, -signal);
        }
    }

    public void UpdateMuscle(Muscle muscle, float targetActivation)
    {
        if (muscle == null) return;
        muscle.activation = Mathf.MoveTowards(
            muscle.activation, targetActivation,
            activationSpeed * Time.fixedDeltaTime);
    }

    // Freeze snap. Muscle.activation is a first-order lag at
    // muscleActivationSpeed 20: reversing a saturated stance extensor
    // takes ~100 ms. The new swing starts the IK from zero, not from
    // the previous owner's saturation.
    public static void Zero(Muscle flexor, Muscle extensor)
    {
        if (flexor != null) flexor.activation = 0f;
        if (extensor != null) extensor.activation = 0f;
    }

    public static void ZeroLeg(Muscle hipFlexor, Muscle hipExtensor,
                              Muscle kneeFlexor, Muscle kneeExtensor)
    {
        Zero(hipFlexor, hipExtensor);
        Zero(kneeFlexor, kneeExtensor);
    }

    // Stable PD to a joint angle. Same formula as the neck, sign as
    // ControlJointToAngle: P*error − D*speed. Denominator 1+K·Δt/I always.
    // directionalDriveTorque: build the denominator from the muscle this
    // step will actually engage instead of always from the flexor.
    public float JointStable(HingeJoint2D joint, float targetAngle,
                             float pGain, float dGain, float errorRef,
                             Muscle flexor, Muscle extensor, ref float cachedInertia,
                             bool directionalDriveTorque = false)
    {
        if (joint == null || flexor == null || extensor == null) return 0f;

        if (joint.useLimits)
            targetAngle = Mathf.Clamp(targetAngle, joint.limits.min, joint.limits.max);

        float dt = Time.fixedDeltaTime;
        float angleForError = joint.jointAngle + joint.jointSpeed * dt;

        float error = Mathf.DeltaAngle(angleForError, targetAngle) / Mathf.Max(1f, errorRef);
        float speed = joint.jointSpeed / speedReference;
        float raw = pGain * error - dGain * speed;
        float driveTorque = directionalDriveTorque && raw > 0f
            ? extensor.maxTorque
            : flexor.maxTorque;

        float damping = 0f;
        if (cachedInertia <= 0f)
            cachedInertia = EffectiveInertia(joint);
        if (cachedInertia > 0f)
        {
            float k = dGain * driveTorque * Mathf.Rad2Deg
                      / Mathf.Max(1f, speedReference);
            damping = k * dt / cachedInertia;
        }

        float demand = raw / (1f + damping);
        float signal = Mathf.Clamp(demand, -1f, 1f);
        lastDemand = demand;
        ApplySignal(signal, flexor, extensor);
        return signal;
    }

    // Loaded-hip target cap. A planted thigh is tied to the slab, so hip
    // torque is a moment on the whole body about the contact. Past m*g*arm
    // the sole rotates instead. Only the position term is bounded; damping
    // may still oppose motion. A cap of zero means "no ceiling".
    public static float CapLoadedHip(HingeJoint2D joint, Muscle flexor, Muscle extensor,
                                      float targetDeg, in HipCap cap)
    {
        if (joint == null || cap.pGain <= 0.0001f) return targetDeg;
        float maxTorque = Mathf.Max(flexor != null ? flexor.maxTorque : 0f,
                                      extensor != null ? extensor.maxTorque : 0f);
        if (maxTorque <= 0.001f) return targetDeg;
        float heelBand = HipTorqueBandDeg(cap.heelNm, maxTorque, cap.pGain, cap.errorRef);
        float toeBand = HipTorqueBandDeg(cap.toeNm, maxTorque, cap.pGain, cap.errorRef);
        float measured = joint.jointAngle + joint.jointSpeed * Time.fixedDeltaTime;
        return Mathf.Clamp(targetDeg, measured - toeBand, measured + heelBand);
    }

    public static float HipTorqueBandDeg(float capNm, float maxTorque,
                                           float pGain, float errorRef)
    {
        if (capNm <= 0.0001f) return float.PositiveInfinity;
        return Mathf.Clamp01(capNm / maxTorque)
               * Mathf.Max(1f, errorRef)
               / Mathf.Max(0.0001f, pGain);
    }

    // Ankle CoM loop: torque against offset, not a spring to an angle.
    // A command into the last limitMargin degrees of the stop is zeroed.
    // WALK CYCLE 126: returns the signal actually applied (post the stop-margin
    // protect) so the pose path's instrument can record it. Callers that only
    // drive may ignore the return; the arithmetic is unchanged.
    public float ApplyAnkle(HingeJoint2D joint, float balanceCommand,
                           Muscle flexor, Muscle extensor, float limitMargin)
    {
        if (joint == null || flexor == null || extensor == null) return 0f;
        float signal = ProtectJointLimit(joint, Mathf.Clamp(balanceCommand, -1f, 1f),
            limitMargin);
        ApplySignal(signal, flexor, extensor);
        return signal;
    }

    public static float ProtectJointLimit(HingeJoint2D joint, float signal, float margin)
    {
        if (joint == null || !joint.useLimits) return signal;
        float angle = joint.jointAngle;
        float min = joint.limits.min;
        float max = joint.limits.max;
        if (signal > 0f && angle >= max - margin) return 0f;
        if (signal < 0f && angle <= min + margin) return 0f;
        return signal;
    }

    public static float EffectiveInertia(HingeJoint2D joint)
    {
        if (joint == null) return 0f;
        Rigidbody2D self = joint.attachedRigidbody;
        Rigidbody2D connected = joint.connectedBody;
        float a = self != null ? self.inertia : 0f;
        float b = connected != null ? connected.inertia : 0f;
        if (a <= 0f) return b;
        if (b <= 0f) return a;
        return 1f / (1f / a + 1f / b);
    }
}
