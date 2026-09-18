using UnityEngine;

// Human vestibular apparatus.
// Reads torso and pelvis world orientation. Applies no forces.
// Noise, delay, and filtering belong here later.
public class VestibularSystem : MonoBehaviour
{
    [Header("Noise settings (unused for now)")]
    public float tiltNoiseDegrees = 0f;            // tilt measurement noise (degrees)
    public float angularVelocityNoiseDegrees = 0f; // angular-velocity measurement noise (deg/s)

    private Rigidbody2D torsoRb;
    private Rigidbody2D pelvisRb;
    private Rigidbody2D neckRb;
    private Rigidbody2D headRb;

    void Awake()
    {
        Transform torsoTransform = transform.Find("Torso");
        if (torsoTransform == null)
            torsoTransform = transform.Find("Chest");
        if (torsoTransform != null)
        {
            torsoRb = torsoTransform.GetComponent<Rigidbody2D>();
        }
        else
        {
            Debug.LogError("VestibularSystem: Torso/Chest not found!");
        }

        Transform pelvisTransform = transform.Find("Pelvis");
        if (pelvisTransform != null)
        {
            pelvisRb = pelvisTransform.GetComponent<Rigidbody2D>();
        }
        else
        {
            Debug.LogError("VestibularSystem: Pelvis not found!");
        }

        // Neck and head are optional: the body is assembled at runtime.
        Transform neckTransform = transform.Find("Neck");
        if (neckTransform != null) neckRb = neckTransform.GetComponent<Rigidbody2D>();

        Transform headTransform = transform.Find("Head");
        if (headTransform != null) headRb = headTransform.GetComponent<Rigidbody2D>();
    }

    // Current torso tilt from vertical (degrees)
    public float GetBodyTilt()
    {
        if (torsoRb == null) return 0f;
        float tilt = Mathf.DeltaAngle(0f, torsoRb.rotation);
        if (tiltNoiseDegrees > 0f)
            tilt += Random.Range(-tiltNoiseDegrees, tiltNoiseDegrees);
        return tilt;
    }

    // Torso angular velocity (degrees per second)
    public float GetBodyAngularVelocity()
    {
        if (torsoRb == null) return 0f;
        float angVel = torsoRb.angularVelocity;
        if (angularVelocityNoiseDegrees > 0f)
            angVel += Random.Range(-angularVelocityNoiseDegrees, angularVelocityNoiseDegrees);
        return angVel;
    }

    // Pelvis tilt to world vertical (degrees). Same axis as the torso:
    // otherwise the lumbar operating point is invisible, and that is what jams the joint.
    public float GetPelvisTilt()
    {
        if (pelvisRb == null) return 0f;
        float tilt = Mathf.DeltaAngle(0f, pelvisRb.rotation);
        if (tiltNoiseDegrees > 0f)
            tilt += Random.Range(-tiltNoiseDegrees, tiltNoiseDegrees);
        return tilt;
    }

    public float GetPelvisAngularVelocity()
    {
        if (pelvisRb == null) return 0f;
        float angVel = pelvisRb.angularVelocity;
        if (angularVelocityNoiseDegrees > 0f)
            angVel += Random.Range(-angularVelocityNoiseDegrees, angularVelocityNoiseDegrees);
        return angVel;
    }

    // Neck and head tilt to world vertical. In a human, gaze is stabilized
    // by the vestibular reflex, not the angle to the chest: the head must know
    // where up is, or it rides down with a falling torso.
    public float GetNeckTilt()
    {
        if (neckRb == null) return 0f;
        float tilt = Mathf.DeltaAngle(0f, neckRb.rotation);
        if (tiltNoiseDegrees > 0f)
            tilt += Random.Range(-tiltNoiseDegrees, tiltNoiseDegrees);
        return tilt;
    }

    public float GetNeckAngularVelocity()
    {
        if (neckRb == null) return 0f;
        float angVel = neckRb.angularVelocity;
        if (angularVelocityNoiseDegrees > 0f)
            angVel += Random.Range(-angularVelocityNoiseDegrees, angularVelocityNoiseDegrees);
        return angVel;
    }

    public float GetHeadTilt()
    {
        if (headRb == null) return 0f;
        float tilt = Mathf.DeltaAngle(0f, headRb.rotation);
        if (tiltNoiseDegrees > 0f)
            tilt += Random.Range(-tiltNoiseDegrees, tiltNoiseDegrees);
        return tilt;
    }

    public float GetHeadAngularVelocity()
    {
        if (headRb == null) return 0f;
        float angVel = headRb.angularVelocity;
        if (angularVelocityNoiseDegrees > 0f)
            angVel += Random.Range(-angularVelocityNoiseDegrees, angularVelocityNoiseDegrees);
        return angVel;
    }

    // Balance error as a mix of tilt and angular velocity.
    // Weights are set from outside.
    public float GetBalanceError(float tiltWeight = 1f, float angularVelocityWeight = 0.1f)
    {
        return tiltWeight * GetBodyTilt() + angularVelocityWeight * GetBodyAngularVelocity();
    }
}
