using UnityEngine;

// Viscous joint friction via direct torques.
// Formula: M = -K * ω, where ω is relative angular velocity in rad/s.
// Equal and opposite torques go to both bodies,
// matching Newton's third law and keeping the system's net torque.
// ActuatorDriver calls Apply before muscles; FixedUpdate is a fallback.
[RequireComponent(typeof(HingeJoint2D))]
[DefaultExecutionOrder(50)]
public class JointFriction : MonoBehaviour
{
    [Tooltip("Viscous friction coefficient (K). Larger means more resistance.")]
    public float damping = 2f;

    [Tooltip("Maximum friction torque. Caps the force so impacts do not blow the joint apart.")]
    public float maxTorque = 15f;

    // true — ActuatorDriver calls Apply; this FixedUpdate stays silent.
    [System.NonSerialized]
    public bool drivenExternally;

    private HingeJoint2D joint;
    private Rigidbody2D rbSelf;

    // 1/I₁ + 1/I₂. In Awake Rigidbody2D.inertia is not yet from the collider —
    // cache only on the first Apply (same moment SPD computes lazily).
    private float cachedInvInertia;
    private bool invInertiaCached;

    void Awake()
    {
        joint = GetComponent<HingeJoint2D>();
        rbSelf = GetComponent<Rigidbody2D>();

        if (joint == null || rbSelf == null)
        {
            Debug.LogError("JointFriction must be on the same object as HingeJoint2D and Rigidbody2D.");
        }
    }

    void FixedUpdate()
    {
        if (drivenExternally) return;
        Apply();
    }

    public void Apply()
    {
        if (joint == null || joint.connectedBody == null || rbSelf == null) return;

        // Relative speed: Unity stores angularVelocity in degrees/s.
        float relativeAngularVelocityRad =
            (rbSelf.angularVelocity - joint.connectedBody.angularVelocity) * Mathf.Deg2Rad;

        // The pair τ, −τ gives relative acceleration τ·(1/I₁ + 1/I₂).
        // Kinematic and static bodies have inertia = 0 — leave them out of the sum.
        if (!invInertiaCached)
        {
            cachedInvInertia = 0f;
            if (rbSelf.inertia > 0f) cachedInvInertia += 1f / rbSelf.inertia;
            float connectedInertia = joint.connectedBody.inertia;
            if (connectedInertia > 0f) cachedInvInertia += 1f / connectedInertia;
            invInertiaCached = true;
        }
        if (cachedInvInertia <= 0f) return;

        float torque = -damping * relativeAngularVelocityRad;

        // An explicit damper is stable only at K·Δt/I < 2. At the neck this is exceeded
        // 57 times, at the elbow 4 — the maxTorque ceiling only hid divergence,
        // turning it into a limit cycle. τ_max kills relative speed
        // in one step and does not overshoot through zero.
        float stopTorque = Mathf.Abs(relativeAngularVelocityRad) / (cachedInvInertia * Time.fixedDeltaTime);
        torque = Mathf.Clamp(torque, -stopTorque, stopTorque);
        torque = Mathf.Clamp(torque, -maxTorque, maxTorque);

        rbSelf.AddTorque(torque, ForceMode2D.Force);
        joint.connectedBody.AddTorque(-torque, ForceMode2D.Force);
    }
}
