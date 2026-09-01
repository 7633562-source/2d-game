using UnityEngine;

// Passive wood torsion spring. Not a muscle: no activation.
// holdTorque cancels wood self-weight at the grow pose so branches
// do not droop to the hinge limit. The spring only fights extra load
// (a bird). Third law: τ, −τ. TreeDriver owns the step.
[RequireComponent(typeof(HingeJoint2D))]
[DefaultExecutionOrder(50)]
public class TreeSpring : MonoBehaviour
{
    [Tooltip("Stiffness, N·m/rad. Extra load (a bird) bends the joint.")]
    public float stiffness = 20f;

    [Tooltip("Joint rest angle, degrees. Build places the segment already rotated, rest = 0.")]
    public float restAngle = 0f;

    [Tooltip("World Z rotation of the grow pose, degrees. 0 is up.")]
    public float restWorldAngle = 0f;

    [Tooltip("Stiffness of the world-pose hold, N·m/rad. Independent of jointAngle sign.")]
    public float worldStiffness = 40f;

    [Tooltip("Feedforward that holds the grow pose against wood gravity, N·m.")]
    public float holdTorque = 0f;

    [Tooltip("Cap on the spring part only. Hold is applied in full.")]
    public float maxTorque = 40f;

    [System.NonSerialized]
    public bool drivenExternally;

    private HingeJoint2D joint;
    private Rigidbody2D rbSelf;
    private float cachedInvInertia;
    private bool invInertiaCached;

    void Awake()
    {
        joint = GetComponent<HingeJoint2D>();
        rbSelf = GetComponent<Rigidbody2D>();
    }

    void FixedUpdate()
    {
        if (drivenExternally) return;
        Apply();
    }

    public void Apply()
    {
        if (joint == null || joint.connectedBody == null || rbSelf == null) return;

        if (!invInertiaCached)
        {
            cachedInvInertia = 0f;
            if (rbSelf.inertia > 0f) cachedInvInertia += 1f / rbSelf.inertia;
            float connectedInertia = joint.connectedBody.inertia;
            if (connectedInertia > 0f) cachedInvInertia += 1f / connectedInertia;
            invInertiaCached = true;
        }
        if (cachedInvInertia <= 0f) return;

        // Human hinges: +AddTorque (CCW) decreases jointAngle. A textbook
        // −k·θ spring slams every fork into the +limit.
        float errorRad = (joint.jointAngle - restAngle) * Mathf.Deg2Rad;
        float springTorque = stiffness * errorRad;

        // One-step caps assume muscle-scale inertia. Wood boxes sit near
        // minSegmentInertia: springStop was then < 1 N·m and gravity
        // walked every fork to the hinge limit. Cap by maxTorque only.
        // No world-pose term: τ, −τ on a world error yanks the parent
        // to the stop while the child looks upright (crown height lies).
        springTorque = Mathf.Clamp(springTorque, -maxTorque, maxTorque);

        float torque = holdTorque + springTorque;
        rbSelf.AddTorque(torque, ForceMode2D.Force);
        joint.connectedBody.AddTorque(-torque, ForceMode2D.Force);
    }
}
