using System.Collections.Generic;
using UnityEngine;

// Sit pad on a tree. Wood is ghost; this collider is a trigger, not a shelf.
// The flock body attaches in FixedUpdate — no solver contact with the twig.
// After TreeDriver look-sway (order 50): snap the sitter so the crow rides
// the picture, not the frozen wood pose.
[DefaultExecutionOrder(60)]
public class BirdPerch : MonoBehaviour
{
    // OverlapBox on Ground hits dirt too. Map size = pad count, not every fixture.
    private static readonly Dictionary<Collider2D, BirdPerch> byColliderId = new Dictionary<Collider2D, BirdPerch>(64);

    private Transform lookFollow;
    private Vector3 lookRestLocalPos;
    private Quaternion lookRestLocalRot = Quaternion.identity;
    private Vector3 padRestLocalPos;
    private bool lookBound;
    private BirdController occupant;
    private Collider2D registeredCollider;
    private Collider2D lastOfferHit;
    private Bird lastOfferBird;

    void OnEnable()
    {
        RegisterCollider();
    }

    void OnDisable()
    {
        UnregisterCollider();
    }

    void Start()
    {
        MakeTrigger();
        RegisterCollider();
        BindLookFollow();
    }

    public void MakeTrigger()
    {
        Collider2D col = GetComponent<Collider2D>();
        if (col != null)
            col.isTrigger = true;
    }

    public static BirdPerch FromCollider(Collider2D hit)
    {
        if (hit == null)
            return null;
        BirdPerch pad;
        if (!byColliderId.TryGetValue(hit, out pad))
            return null;
        return pad != null ? pad : null;
    }

    private void RegisterCollider()
    {
        Collider2D col = GetComponent<Collider2D>();
        if (col == null)
            return;
        if (registeredCollider == col && byColliderId.ContainsKey(col))
            return;
        UnregisterCollider();
        registeredCollider = col;
        byColliderId[col] = this;
    }

    private void UnregisterCollider()
    {
        if (registeredCollider == null)
            return;
        byColliderId.Remove(registeredCollider);
        registeredCollider = null;
    }

    public void BindOccupant(BirdController bird)
    {
        occupant = bird;
    }

    public void ClearOccupant(BirdController bird)
    {
        if (occupant == bird)
            occupant = null;
    }

    void LateUpdate()
    {
        if (occupant != null)
            occupant.FollowPerchVisual();
    }

    // B-PERCH-1 (25.09, bird lane): seat height offset, m. Negative = the bird
    // sits lower on the pad (claws on the wood's top edge, not on the leaf pads
    // that cover the ring marks inside the crown). 0 = the accepted pose.
    public float seatDy;

    // B-PERCH-12 (26.09): the hold's own x window, m. The sitter keeps its own
    // x while the arrival x sits inside the window; outside it the pad centres
    // the bird on the mark's own x. 0.4 is the accepted behaviour (every take
    // before B-PERCH-12); the trigger narrows it while the claws' own column
    // disagrees with the pad's band (a drop past the limb's end - take
    // bPCH12a's 0.58 m seat teleport). Picture/physics: the same hold the pad
    // machinery always wrote, only its x.
    public float holdXWin = HoldXWinFar;
    public const float HoldXWinFar = 0.4f;

    // Body Y so the (trigger) perch volume sits on the pad, not the breast.
    public Vector2 HoldBodyPosition(Bird bird, float bodyX)
    {
        Vector2 pad = CurrentPadWorld();
        float x = Mathf.Abs(bodyX - pad.x) < holdXWin ? bodyX : pad.x;
        if (bird != null && bird.perchCollider != null)
        {
            float bottom = bird.perchCollider.offset.y - bird.perchCollider.size.y * 0.5f;
            return new Vector2(x, pad.y - bottom + seatDy);
        }

        float y = bird != null ? bird.StandingRootOffset(pad.y) : pad.y;
        return new Vector2(x, y);
    }

    // Wood may stay put (Static Apple) while Visual look-sways.
    // Sway wood moves the pad itself; then this matches transform.position.
    public Vector2 CurrentPadWorld()
    {
        BindLookFollow();
        Transform wood = transform.parent;
        if (lookFollow == null || wood == null)
            return transform.position;

        Vector3 padRest = wood.TransformPoint(padRestLocalPos);
        Vector3 lookRest = wood.TransformPoint(lookRestLocalPos);
        Quaternion lookRestWorld = wood.rotation * lookRestLocalRot;
        Vector3 local = Quaternion.Inverse(lookRestWorld) * (padRest - lookRest);
        return lookFollow.position + lookFollow.rotation * local;
    }

    public Quaternion CurrentLookRotation()
    {
        BindLookFollow();
        if (lookFollow != null)
            return lookFollow.rotation;
        Transform wood = transform.parent;
        return wood != null ? wood.rotation : transform.rotation;
    }

    private void BindLookFollow()
    {
        if (lookBound)
            return;
        lookBound = true;
        padRestLocalPos = transform.localPosition;
        Transform wood = transform.parent;
        if (wood == null)
            return;
        HumanSegment seg = wood.GetComponent<HumanSegment>();
        lookFollow = seg != null ? seg.visual : wood.Find("Visual");
        if (lookFollow == null)
            return;
        lookRestLocalPos = lookFollow.localPosition;
        lookRestLocalRot = lookFollow.localRotation;
    }

    void OnDestroy()
    {
        occupant = null;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        Offer(other);
    }

    void OnTriggerStay2D(Collider2D other)
    {
        // Occupant already holds this pad; TickFlock keeps it without a parent walk.
        if (occupant != null)
            return;
        Offer(other);
    }

    private void Offer(Collider2D other)
    {
        if (other == null)
            return;
        Bird bird;
        if (other == lastOfferHit)
            bird = lastOfferBird;
        else
        {
            lastOfferHit = other;
            lastOfferBird = other.GetComponentInParent<Bird>();
            bird = lastOfferBird;
        }
        if (bird == null || bird.controller == null)
            return;
        bird.controller.OfferPerch(this);
    }
}
