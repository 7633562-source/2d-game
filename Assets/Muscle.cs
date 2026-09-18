using UnityEngine;

// Muscle that actively produces joint torque.
// It sits on top of passive friction (JointFriction) and can
// flex or extend the joint depending on its direction.
// ActuatorDriver applies the torque (after friction); FixedUpdate is a fallback.
[DefaultExecutionOrder(50)]
public class Muscle : MonoBehaviour
{
    [Tooltip("Maximum torque the muscle can produce (at activation = 1).")]
    public float maxTorque = 50f;

    [Tooltip("Current muscle activation from 0 to 1. Driven by outside code.")]
    [Range(0f, 1f)]
    public float activation = 0f;

    [Tooltip("Muscle direction: +1 flexes the joint, -1 extends it.")]
    public float direction = 1f;

    // true — ActuatorDriver calls Apply; this FixedUpdate stays silent.
    [System.NonSerialized]
    public bool drivenExternally;

    private HingeJoint2D joint;
    private Rigidbody2D rbSelf;

    void Awake()
    {
        joint = GetComponent<HingeJoint2D>();
        rbSelf = GetComponent<Rigidbody2D>();

        if (joint == null || rbSelf == null)
        {
            Debug.LogError("Muscle must be on the same object as HingeJoint2D and Rigidbody2D.");
        }
    }

    void FixedUpdate()
    {
        if (drivenExternally) return;
        Apply();
    }

    public void Apply()
    {
        if (joint == null || joint.connectedBody == null) return;

        float torque = activation * maxTorque * direction;

        rbSelf.AddTorque(torque, ForceMode2D.Force);
        joint.connectedBody.AddTorque(-torque, ForceMode2D.Force);
    }
}
