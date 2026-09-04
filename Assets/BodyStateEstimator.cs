using UnityEngine;
using System.Collections.Generic;

// Sensor layer: reads body state and does not control it.
[DefaultExecutionOrder(-100)]
public class BodyStateEstimator : MonoBehaviour
{
    // ─── OTHER COMPONENT REFS ───
    private VestibularSystem vestibular;
    private CenterOfMassCalculator comCalculator;

    // ─── JOINT REFS ───
    private HingeJoint2D leftHipJoint;
    private HingeJoint2D rightHipJoint;
    private HingeJoint2D leftKneeJoint;
    private HingeJoint2D rightKneeJoint;
    private HingeJoint2D leftAnkleJoint;
    private HingeJoint2D rightAnkleJoint;
    private HingeJoint2D lumbarJoint;

    // ─── FOOT REFS ───
    private Transform leftFoot;
    private Transform rightFoot;
    private Collider2D leftFootCollider;
    private Collider2D rightFootCollider;

    private Transform leftHand;
    private Transform rightHand;
    private Collider2D leftHandCollider;
    private Collider2D rightHandCollider;
    private Collider2D groundCollider;
    private BalanceController balance;
    private MotionIntent intent;

    [Header("Contact probe")]
    [Tooltip("Probe thickness under the foot's bottom edge (units). OverlapBox only.")]
    public float groundProbeThickness = 0.06f;
    [Tooltip("GetContacts NonAlloc instead of OverlapBox under the foot. Revert is false.")]
    public bool useGetContactsProbe = true;

    // Reused buffers: no new on every physics step.
    private readonly Collider2D[] groundProbeHits = new Collider2D[8];
    private readonly ContactPoint2D[] groundContacts = new ContactPoint2D[8];
    private ContactFilter2D groundContactFilter;
    private bool groundContactFilterReady;

    // Margin ruler: saturation of the 8-slot buffer is a hidden ceiling for future colliders.
    // Counters only; they do not affect grounded.
    public int probeCallCount;
    public int probeSaturatedCount;
    public int probeHitSum;

    // ─── STATE DATA (public for logging) ───
    [Header("Center of mass")]
    public Vector2 comPosition;
    public Vector2 comVelocity;

    [Header("Support")]
    public float supportMinX;
    public float supportMaxX;
    public float supportCenterX;
    public float supportWidth;
    public float supportMargin; // + inside support, 0 on the edge, - outside

    [Header("Feet")]
    public Vector2 leftFootPosition;
    public Vector2 rightFootPosition;
    public bool leftFootGrounded;
    public bool rightFootGrounded;
    public float leftFootGroundClearance;
    public float rightFootGroundClearance;
    // Heel contact (point.x ≤ foot center). Unused by CoM/fell.
    public bool leftFootHeelLoaded;
    public bool rightFootHeelLoaded;
    public bool leftHandGrounded;
    public bool rightHandGrounded;

    [Header("Joint angles (degrees)")]
    public float leftHipAngle;
    public float rightHipAngle;
    public float leftKneeAngle;
    public float rightKneeAngle;
    public float leftAnkleAngle;
    public float rightAnkleAngle;
    public float lumbarAngle;

    [Header("Joint angular velocities (deg/s)")]
    public float leftHipAngularVelocity;
    public float rightHipAngularVelocity;
    public float leftKneeAngularVelocity;
    public float rightKneeAngularVelocity;
    public float leftAnkleAngularVelocity;
    public float rightAnkleAngularVelocity;
    public float lumbarAngularVelocity;

    [Header("Vestibular system")]
    public float torsoTilt;               // torso tilt (degrees)
    public float torsoAngularVelocity;     // torso angular velocity (deg/s)
    public float pelvisTilt;              // pelvis tilt to world vertical (degrees)
    public float pelvisAngularVelocity;   // pelvis angular velocity (deg/s)

    // ─── INTERNALS FOR COM VELOCITY ───
    private Vector2 previousComPosition;
    private bool hasPreviousCom = false;

    // ─── AWAKE: RESOLVE REFS ───
    void Awake()
    {
        vestibular = GetComponent<VestibularSystem>();
        comCalculator = GetComponent<CenterOfMassCalculator>();

        leftHipJoint = GetJoint("LeftLegThigh");
        rightHipJoint = GetJoint("RightLegThigh");
        leftKneeJoint = GetJoint("LeftLegShin");
        rightKneeJoint = GetJoint("RightLegShin");
        leftAnkleJoint = GetJoint("LeftLegFoot");
        rightAnkleJoint = GetJoint("RightLegFoot");
        lumbarJoint = GetJoint("Torso");

        leftFoot = transform.Find("LeftLegFoot");
        rightFoot = transform.Find("RightLegFoot");
        leftFootCollider = leftFoot != null ? leftFoot.GetComponent<Collider2D>() : null;
        rightFootCollider = rightFoot != null ? rightFoot.GetComponent<Collider2D>() : null;

        leftHand = transform.Find("LeftArmHand");
        rightHand = transform.Find("RightArmHand");
        leftHandCollider = leftHand != null ? leftHand.GetComponent<Collider2D>() : null;
        rightHandCollider = rightHand != null ? rightHand.GetComponent<Collider2D>() : null;
        balance = GetComponent<BalanceController>();
        intent = GetComponent<MotionIntent>();
        GameObject ground = GameObject.Find("Ground");
        groundCollider = ground != null ? ground.GetComponent<Collider2D>() : null;

        if (comCalculator == null)
        {
            Debug.LogError("BodyStateEstimator: CenterOfMassCalculator not found!");
        }
        if (vestibular == null)
        {
            Debug.LogError("BodyStateEstimator: VestibularSystem not found!");
        }
    }

    // ─── FIND JOINT BY CHILD NAME ───
    private HingeJoint2D GetJoint(string childName)
    {
        Transform t = transform.Find(childName);
        return t != null ? t.GetComponent<HingeJoint2D>() : null;
    }

    // ─── FIXEDUPDATE: COLLECT DATA ───
    void FixedUpdate()
    {
        // 1. Center of mass
        if (comCalculator != null)
        {
            comPosition = comCalculator.GetCenterOfMass();

            // CoM velocity = (current - previous) / dt
            if (hasPreviousCom)
            {
                comVelocity = (comPosition - previousComPosition) / Time.fixedDeltaTime;
            }
            else
            {
                comVelocity = Vector2.zero;
                hasPreviousCom = true;
            }
            previousComPosition = comPosition;
        }

        // 2. Foot positions
        if (leftFoot != null) leftFootPosition = leftFoot.position;
        if (rightFoot != null) rightFootPosition = rightFoot.position;

        // 3. Foot-ground contact (heel is separate and does not affect grounded)
        leftFootGrounded = IsSegmentGrounded(leftFootCollider, out leftFootHeelLoaded);
        rightFootGrounded = IsSegmentGrounded(rightFootCollider, out rightFootHeelLoaded);
        float groundTopY = ResolveGroundTopY();
        leftFootGroundClearance = FootGroundClearance(leftFootCollider, groundTopY);
        rightFootGroundClearance = FootGroundClearance(rightFootCollider, groundTopY);
        // Skip hands while the crouch is shallow: in stance the arms are in the air,
        // and OverlapBox still walked their colliders every step.
        float slop = balance != null ? Mathf.Max(0f, balance.crouchHandGroundSlop) : 0.06f;
        float handGroundY = groundTopY + slop;
        leftHandGrounded = IsHandNearGround(leftHandCollider, handGroundY);
        rightHandGrounded = IsHandNearGround(rightHandCollider, handGroundY);
        if (balance != null && balance.crouchLevel >= balance.crouchHandSupportMin)
        {
            leftHandGrounded = true;
            rightHandGrounded = true;
        }

        // 4. Support bounds and stability margin
        CalculateSupport();

        // 5. Joint angles
        leftHipAngle = leftHipJoint != null ? leftHipJoint.jointAngle : 0f;
        rightHipAngle = rightHipJoint != null ? rightHipJoint.jointAngle : 0f;
        leftKneeAngle = leftKneeJoint != null ? leftKneeJoint.jointAngle : 0f;
        rightKneeAngle = rightKneeJoint != null ? rightKneeJoint.jointAngle : 0f;
        leftAnkleAngle = leftAnkleJoint != null ? leftAnkleJoint.jointAngle : 0f;
        rightAnkleAngle = rightAnkleJoint != null ? rightAnkleJoint.jointAngle : 0f;
        lumbarAngle = lumbarJoint != null ? lumbarJoint.jointAngle : 0f;

        // 6. Joint angular velocities
        leftHipAngularVelocity = leftHipJoint != null ? leftHipJoint.jointSpeed : 0f;
        rightHipAngularVelocity = rightHipJoint != null ? rightHipJoint.jointSpeed : 0f;
        leftKneeAngularVelocity = leftKneeJoint != null ? leftKneeJoint.jointSpeed : 0f;
        rightKneeAngularVelocity = rightKneeJoint != null ? rightKneeJoint.jointSpeed : 0f;
        leftAnkleAngularVelocity = leftAnkleJoint != null ? leftAnkleJoint.jointSpeed : 0f;
        rightAnkleAngularVelocity = rightAnkleJoint != null ? rightAnkleJoint.jointSpeed : 0f;
        lumbarAngularVelocity = lumbarJoint != null ? lumbarJoint.jointSpeed : 0f;

        // 7. Vestibular data: torso and pelvis in the same reference frame
        if (vestibular != null)
        {
            torsoTilt = vestibular.GetBodyTilt();
            torsoAngularVelocity = vestibular.GetBodyAngularVelocity();
            pelvisTilt = vestibular.GetPelvisTilt();
            pelvisAngularVelocity = vestibular.GetPelvisAngularVelocity();
        }
    }

    // ─── FOOT-GROUND CONTACT ───
    // GetContacts — PhysX contacts with the Ground layer. OverlapBox is a geometric
    // 0.06 m probe under the edge (useGetContactsProbe = false).
    // heelLoaded: a contact with point.x ≤ foot center (human faces +X).
    private bool IsSegmentGrounded(Collider2D col, out bool heelLoaded)
    {
        heelLoaded = false;
        if (col == null) return false;

        int hitCount;
        if (useGetContactsProbe)
        {
            if (!groundContactFilterReady)
            {
                groundContactFilter = new ContactFilter2D();
                groundContactFilter.SetLayerMask(GroundLayers.Mask);
                groundContactFilter.useTriggers = false;
                groundContactFilterReady = true;
            }
            hitCount = col.GetContacts(groundContactFilter, groundContacts);
            float midX = col.bounds.center.x;
            int n = Mathf.Min(hitCount, groundContacts.Length);
            for (int i = 0; i < n; i++)
            {
                if (groundContacts[i].point.x <= midX)
                {
                    heelLoaded = true;
                    break;
                }
            }
        }
        else
        {
            Bounds bounds = col.bounds;
            Vector2 probeCenter = new Vector2(bounds.center.x, bounds.min.y);
            Vector2 probeSize = new Vector2(bounds.size.x * 0.9f, groundProbeThickness);
            hitCount = Physics2D.OverlapBoxNonAlloc(
                probeCenter, probeSize, 0f, groundProbeHits, GroundLayers.Mask);
            // Without contact points the heel cannot be told apart — treat any contact as heel.
            heelLoaded = hitCount > 0;
        }

        probeCallCount++;
        probeHitSum += hitCount;
        int bufLen = useGetContactsProbe ? groundContacts.Length : groundProbeHits.Length;
        if (hitCount >= bufLen)
            probeSaturatedCount++;
        return hitCount > 0;
    }

    // Hand: PhysX probe plus a y=−2.0 slop in a deep squat — the palm
    // does not always punch the collider, but support in X is already widened.
    private bool IsHandNearGround(Collider2D col, float handGroundY)
    {
        if (col == null) return false;
        bool heelIgnored;
        if (IsSegmentGrounded(col, out heelIgnored)) return true;
        return col.bounds.min.y <= handGroundY;
    }

    private float ResolveGroundTopY()
    {
        if (groundCollider == null)
        {
            GameObject ground = GameObject.Find("Ground");
            if (ground != null)
                groundCollider = ground.GetComponent<Collider2D>();
        }
        return groundCollider != null ? groundCollider.bounds.max.y : -2f;
    }

    private static float FootGroundClearance(Collider2D col, float groundTopY)
    {
        if (col == null) return 0.5f;
        return col.bounds.min.y - groundTopY;
    }

    // ─── SUPPORT REGION AND STABILITY MARGIN ───
    // Support is the union of heel and toe colliders that touch the ground.
    private void CalculateSupport()
    {
        if (leftFoot == null || rightFoot == null)
        {
            supportMinX = 0f;
            supportMaxX = 0f;
            supportCenterX = 0f;
            supportWidth = 0f;
            supportMargin = 0f;
            return;
        }

        bool hasSupport = false;
        float minX = 0f;
        float maxX = 0f;

        ExpandSupport(leftFootCollider, leftFootGrounded, ref hasSupport, ref minX, ref maxX);
        ExpandSupport(rightFootCollider, rightFootGrounded, ref hasSupport, ref minX, ref maxX);

        // In a squat the hands on the ground are the third and fourth support points.
        if (balance != null && balance.crouchLevel >= balance.crouchHandSupportMin)
        {
            ExpandSupport(leftHandCollider, leftHandGrounded, ref hasSupport, ref minX, ref maxX);
            ExpandSupport(rightHandCollider, rightHandGrounded, ref hasSupport, ref minX, ref maxX);
        }

        // Both feet in the air: no support; margin is from the midpoint of the feet.
        if (!hasSupport)
        {
            supportCenterX = (leftFoot.position.x + rightFoot.position.x) * 0.5f;
            supportMinX = supportCenterX;
            supportMaxX = supportCenterX;
            supportWidth = 0f;
            supportMargin = -Mathf.Abs(comPosition.x - supportCenterX);
            return;
        }

        supportMinX = minX;
        supportMaxX = maxX;
        supportCenterX = (supportMinX + supportMaxX) * 0.5f;
        supportWidth = supportMaxX - supportMinX;

        // If support width is zero, margin = CoM distance to the center
        if (supportWidth < 0.001f)
        {
            supportMargin = -Mathf.Abs(comPosition.x - supportCenterX);
            return;
        }

        if (comPosition.x >= supportMinX && comPosition.x <= supportMaxX)
        {
            // CoM inside support: margin = minimum distance to the edges
            float distLeft = comPosition.x - supportMinX;
            float distRight = supportMaxX - comPosition.x;
            supportMargin = Mathf.Min(distLeft, distRight);
        }
        else
        {
            // CoM outside: margin = -distance to the nearest edge
            float distToLeft = Mathf.Abs(comPosition.x - supportMinX);
            float distToRight = Mathf.Abs(comPosition.x - supportMaxX);
            supportMargin = -Mathf.Min(distToLeft, distToRight);
        }
    }

    private static void ExpandSupport(Collider2D col, bool grounded, ref bool hasSupport, ref float minX, ref float maxX)
    {
        if (col == null || !grounded) return;
        Bounds b = col.bounds;
        minX = hasSupport ? Mathf.Min(minX, b.min.x) : b.min.x;
        maxX = hasSupport ? Mathf.Max(maxX, b.max.x) : b.max.x;
        hasSupport = true;
    }
}
