using UnityEngine;
using System.Collections.Generic;

// Whole-body center of mass and its projection relative to support.
// Uses every Rigidbody2D on child segments (added in HumanSegment).
public class CenterOfMassCalculator : MonoBehaviour
{
    private List<Rigidbody2D> bodies = new List<Rigidbody2D>();

    private Collider2D leftFootCollider;
    private Collider2D rightFootCollider;
    private Collider2D leftToeCollider;
    private Collider2D rightToeCollider;
    private Collider2D leftHandCollider;
    private Collider2D rightHandCollider;
    private BalanceController balance;
    private BodyStateEstimator bodyState;

    // Last known support: with both feet in the air the midpoint of the feet
    // must not be used — the reference jumps at the most dangerous moment.
    private bool hasLastSupport;
    private float lastMinX;
    private float lastMaxX;

    // On one physics step GetCenterOfMass is called by both the sensor and the controller —
    // a cache keyed by fixedTime skips a second pass over 16 bodies.
    private float cachedCoMFixedTime = -1f;
    private Vector2 cachedCoM;

    // How many child rigidbodies Initialize collected. 0 means
    // GetCenterOfMass falls back to the root transform (lean would
    // not move that x). Quiet stance is 16 on a Human.

    // Init: collect child-segment Rigidbody2Ds and the feet once.
    public void Initialize()
    {
        bodies.Clear();
        foreach (Rigidbody2D rb in GetComponentsInChildren<Rigidbody2D>())
        {
            if (rb != null && rb.gameObject != gameObject)
                bodies.Add(rb);
        }

        Transform leftFoot = transform.Find("LeftLegFoot");
        Transform rightFoot = transform.Find("RightLegFoot");
        leftFootCollider = leftFoot != null ? leftFoot.GetComponent<Collider2D>() : null;
        rightFootCollider = rightFoot != null ? rightFoot.GetComponent<Collider2D>() : null;
        Transform leftToe = transform.Find("LeftLegToe");
        Transform rightToe = transform.Find("RightLegToe");
        leftToeCollider = leftToe != null ? leftToe.GetComponent<Collider2D>() : null;
        rightToeCollider = rightToe != null ? rightToe.GetComponent<Collider2D>() : null;
        Transform leftHand = transform.Find("LeftArmHand");
        Transform rightHand = transform.Find("RightArmHand");
        leftHandCollider = leftHand != null ? leftHand.GetComponent<Collider2D>() : null;
        rightHandCollider = rightHand != null ? rightHand.GetComponent<Collider2D>() : null;
        balance = GetComponent<BalanceController>();
        bodyState = GetComponent<BodyStateEstimator>();
        hasLastSupport = false;
        cachedCoMFixedTime = -1f;
    }

    public int TrackedBodyCount => bodies != null ? bodies.Count : 0;

    private BodyStateEstimator BodyState =>
        bodyState != null ? bodyState : (bodyState = GetComponent<BodyStateEstimator>());

    // World-space center of mass
    public Vector2 GetCenterOfMass()
    {
        if (Time.fixedTime == cachedCoMFixedTime)
            return cachedCoM;

        if (bodies.Count == 0)
        {
            cachedCoM = (Vector2)transform.position;
            cachedCoMFixedTime = Time.fixedTime;
            return cachedCoM;
        }

        Vector2 weightedSum = Vector2.zero;
        float totalMass = 0f;

        foreach (var rb in bodies)
        {
            if (rb == null) continue;
            weightedSum += rb.position * rb.mass;
            totalMass += rb.mass;
        }

        cachedCoM = totalMass < 0.001f ? (Vector2)transform.position : weightedSum / totalMass;
        cachedCoMFixedTime = Time.fixedTime;
        return cachedCoM;
    }

    // CoM offset from mid-support, using collider edges of standing feet.
    public float GetCoMOffsetX()
    {
        if (!TryGetSupportBounds(out float minX, out float maxX))
            return 0f;

        Vector2 com = GetCenterOfMass();
        return com.x - (minX + maxX) * 0.5f;
    }

    public float GetSupportWidth()
    {
        if (!TryGetSupportBounds(out float minX, out float maxX))
            return 0.1f;
        return maxX - minX;
    }

    // Same as BodyStateEstimator.CalculateSupport: grounded feet only;
    // both in the air — keep the last known support.
    // Do not duplicate the PhysX probe: BodyStateEstimator (−100) already refreshed the flags.
    private bool TryGetSupportBounds(out float minX, out float maxX)
    {
        minX = 0f;
        maxX = 0f;
        bool found = false;

        ExpandIfSegment(leftFootCollider, true, true, ref found, ref minX, ref maxX);
        ExpandIfSegment(leftToeCollider, true, false, ref found, ref minX, ref maxX);
        ExpandIfSegment(rightFootCollider, false, true, ref found, ref minX, ref maxX);
        ExpandIfSegment(rightToeCollider, false, false, ref found, ref minX, ref maxX);

        if (balance != null && balance.crouchLevel >= balance.crouchHandSupportMin)
        {
            ExpandIfHandGrounded(leftHandCollider, ref found, ref minX, ref maxX);
            ExpandIfHandGrounded(rightHandCollider, ref found, ref minX, ref maxX);
        }

        if (found)
        {
            lastMinX = minX;
            lastMaxX = maxX;
            hasLastSupport = true;
            return true;
        }

        if (hasLastSupport)
        {
            minX = lastMinX;
            maxX = lastMaxX;
            return true;
        }

        return false;
    }

    private void ExpandIfSegment(Collider2D col, bool left, bool heel,
                                 ref bool found, ref float minX, ref float maxX)
    {
        if (col == null || !IsPlateSegmentGrounded(col, left, heel)) return;
        Bounds b = col.bounds;
        minX = found ? Mathf.Min(minX, b.min.x) : b.min.x;
        maxX = found ? Mathf.Max(maxX, b.max.x) : b.max.x;
        found = true;
    }

    private void ExpandIfHandGrounded(Collider2D col, ref bool found, ref float minX, ref float maxX)
    {
        if (col == null || !IsHandGrounded(col)) return;
        Bounds b = col.bounds;
        minX = found ? Mathf.Min(minX, b.min.x) : b.min.x;
        maxX = found ? Mathf.Max(maxX, b.max.x) : b.max.x;
        found = true;
    }

    private bool IsPlateSegmentGrounded(Collider2D col, bool left, bool heel)
    {
        BodyStateEstimator bs = BodyState;
        if (bs == null || col == null) return false;
        if (left)
            return heel ? bs.leftHeelGrounded : bs.leftToeGrounded;
        return heel ? bs.rightHeelGrounded : bs.rightToeGrounded;
    }

    private bool IsHandGrounded(Collider2D col)
    {
        BodyStateEstimator bs = BodyState;
        if (bs == null) return false;
        if (col == leftHandCollider) return bs.leftHandGrounded;
        if (col == rightHandCollider) return bs.rightHandGrounded;
        return false;
    }
}
