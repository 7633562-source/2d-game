using System.Collections.Generic;
using UnityEngine;

// Bird sensor layer: read-only. No forces.
// Does not use the human VestibularSystem / CenterOfMassCalculator —
// those look up Torso and Pelvis by name and silently return zeros on a bird.
[DefaultExecutionOrder(-100)]
public class BirdSensors : MonoBehaviour
{
    [Header("Foot contact probe")]
    public float groundProbeThickness = 0.04f;
    [Tooltip("Above this height over stance the flock does not call OverlapBox: in cruise the probe is always empty.")]
    public float skipProbeAboveHeight = 0.5f;

    [Header("Center of mass")]
    public Vector2 comPosition;
    public Vector2 comVelocity;

    [Header("Body")]
    [Tooltip("Body pitch, degrees. 0 — body horizontal, plus — nose up.")]
    public float bodyPitch;
    public float bodyPitchRate;
    public Vector2 bodyVelocity;
    public float neckTilt;
    public float neckTiltRate;
    public float headTilt;
    public float headTiltRate;

    [Header("Feet")]
    public bool leftFootGrounded;
    public bool rightFootGrounded;
    public bool bothGrounded;
    public bool nearPerch;
    [Tooltip("Grounded on a branch, not dirt under the tree AABB.")]
    public bool treeSupport;
    public float comOffsetX;

    private readonly List<Rigidbody2D> bodies = new List<Rigidbody2D>();
    private Rigidbody2D bodyRb;
    private Rigidbody2D neckRb;
    private Rigidbody2D headRb;
    private Collider2D leftFootCollider;
    private Collider2D rightFootCollider;
    private Collider2D bodyCollider;
    private BoxCollider2D perchCollider;
    private Bird owner;
    private BirdController huntControl;
    private bool huntDirtLatch;
    private readonly Collider2D[] groundProbeHits = new Collider2D[8];
    private Vector2 previousCom;
    private bool hasPreviousCom;

    // Cycle-1 ruler: how many probes and hits in a run.
    public int probeCallCount;
    public int probeHitSum;
    public int probeSaturatedCount;

    // true — BirdController calls Tick; own FixedUpdate returns.
    [System.NonSerialized]
    public bool drivenExternally;

    public void Initialize()
    {
        bodies.Clear();
        foreach (Rigidbody2D childRb in GetComponentsInChildren<Rigidbody2D>())
        {
            if (childRb != null && childRb.gameObject != gameObject)
                bodies.Add(childRb);
        }

        Transform body = transform.Find("Body");
        bodyRb = body != null ? body.GetComponent<Rigidbody2D>() : null;
        Transform neck = transform.Find("Neck");
        neckRb = neck != null ? neck.GetComponent<Rigidbody2D>() : null;
        Transform head = transform.Find("Head");
        headRb = head != null ? head.GetComponent<Rigidbody2D>() : null;

        owner = GetComponent<Bird>();
        bodyCollider = owner != null && owner.bodyCollider != null
            ? owner.bodyCollider
            : (body != null ? body.GetComponent<Collider2D>() : null);
        perchCollider = owner != null ? owner.perchCollider : null;

        Transform leftFoot = transform.Find("LeftLegFoot");
        Transform rightFoot = transform.Find("RightLegFoot");
        leftFootCollider = leftFoot != null ? leftFoot.GetComponent<Collider2D>() : null;
        rightFootCollider = rightFoot != null ? rightFoot.GetComponent<Collider2D>() : null;

        hasPreviousCom = false;
        huntControl = owner != null ? owner.controller : null;
    }

    // BirdController.Awake: BuildFlock assigns owner.controller after
    // this Initialize, so the cache here can be null. One write, no Find.
    public void BindHuntControl(BirdController control)
    {
        huntControl = control;
    }

    void FixedUpdate()
    {
        if (drivenExternally) return;
        Tick();
    }

    public void Tick()
    {
        // Resting flock: Controller already skipped this Tick. Ragdoll
        // leftover Sit would still OverlapBox at 200 Hz (BIRD-12). Hunt
        // is off. Cached huntControl, no Find.
        if (huntControl != null && huntControl.energyRest)
            return;

        comPosition = ComputeCom();
        if (hasPreviousCom)
            comVelocity = (comPosition - previousCom) / Time.fixedDeltaTime;
        else
        {
            comVelocity = Vector2.zero;
            hasPreviousCom = true;
        }
        previousCom = comPosition;

        if (bodyRb != null)
        {
            // DeltaAngle: on a somersault pitch goes past ±90°, and subtraction breaks.
            bodyPitch = Mathf.DeltaAngle(0f, bodyRb.rotation);
            bodyPitchRate = bodyRb.angularVelocity;
            bodyVelocity = bodyRb.linearVelocity;
        }

        if (neckRb != null)
        {
            neckTilt = Mathf.DeltaAngle(0f, neckRb.rotation);
            neckTiltRate = neckRb.angularVelocity;
        }
        if (headRb != null)
        {
            headTilt = Mathf.DeltaAngle(0f, headRb.rotation);
            headTiltRate = headRb.angularVelocity;
        }

        Vector2 supportPos = bodyRb != null ? (Vector2)bodyRb.position : (Vector2)transform.position;
        if (huntControl == null && owner != null)
            huntControl = owner.controller;
        // Hunt dirt is soil. The crown AABB is soil plus twigs — leaving
        // NearPerch true set treeSupport and Insect.OnDirtPlate went false,
        // so Walk/Peck never reached BeakStrike Rel Hostile under an apple.
        // IsHuntSoil covers preferDirtLand flicker, dirt Walk near the
        // plate, and dirt Peck (woodpecker pad drum stays high).
        // HoldSimulatedSupport / OnHuntDirt / walkThrust share that gate
        // so a flicker before Hurt still plants / horizons for HuntBeak
        // Rel Hostile. Pad Peck keeps the perch path. Cached
        // owner.controller, no Find.
        // Foot probe stays OverlapBoxNonAlloc off this path (BIRD-12:
        // no GetContacts). HuntBeak is a world-root Default solid
        // query (Rigidbody2D.position, body-X centred, PeckHold both
        // ways so leftover SetFacing cannot teleport Rel Hostile), not
        // a foot contact. Live HuntBeak is Peck/Attack Dynamic eat only —
        // preferDirtLand Walk no longer teleports that NeverSleep
        // Default body at 200 Hz. Flock hunt Walk still skips the
        // foot box here; leftover treeSupport must not park OnDirtPlate.
        bool huntDirt = huntControl != null && huntControl.IsHuntSoil();
        if (huntDirt)
            huntDirtLatch = true;
        else if (huntDirtLatch)
        {
            // preferDirtLand can drop one probe before Hurt. NearPerch
            // then set treeSupport and OnDirtPlate parked HuntBeak.
            huntDirt = true;
            huntDirtLatch = false;
        }
        nearPerch = huntDirt ? false : PlantTree.NearPerch(supportPos, 0.9f);

        if (!huntDirt && huntControl != null && huntControl.IsPerched)
        {
            leftFootGrounded = true;
            rightFootGrounded = true;
            bothGrounded = true;
            treeSupport = true;
            comOffsetX = ComputeComOffsetX();
            return;
        }

        if (huntDirt)
        {
            // Hunt dirt is the plate height, not a pad / foot OverlapBox.
            // Ragdoll soles sit in the same soil film as the Insect; a
            // foot probe here is not GetContacts (BIRD-12) but it is
            // wasted at 200 Hz. DirtHuntHeight 0.45 m is the brain's
            // Walk/Peck approach — treating it as grounded hovered a
            // leg above the Insect. Both feet mean soil, ~probe thickness.
            // Physics pose, not interpolated transform — OnHuntDirt uses
            // the same rb.position so Walk thrust and the beak slab agree.
            float y = bodyRb != null ? bodyRb.position.y : transform.position.y;
            float height = y - Bird.StanceRootY(owner);
            // Leftover Fly punched through (flock feet are triggers).
            // bothGrounded at y = −3 still looked like soil, Walk never
            // closed on the Insect, BeakStrike never Rel Hostile.
            // HuntSoilFilm matches the hunt snap (plant below 4 cm) plus
            // leftover Fly jitter — 6 cm dropped bothGrounded at freeze.
            // Foot probe stays OverlapBoxNonAlloc off the hunt path
            // (BIRD-12: no GetContacts). Do not read Collider2D.bounds
            // here: autoSyncTransforms is off, and that AABB is not
            // HuntBeak. Ankles are off on preferDirtLand.
            bool plate = height < BirdController.HuntSoilFilm
                && height > -BirdController.HuntSoilFilm;
            leftFootGrounded = plate;
            rightFootGrounded = plate;
            bothGrounded = plate;
            treeSupport = false;
            comOffsetX = 0f;
            return;
        }
        else if (leftFootCollider == null && rightFootCollider == null)
        {
            // Physics pose, same as hunt dirt. Interpolated transform.y
            // flickered OnDirtPlate and BeakStrike missed Rel Hostile.
            float y = bodyRb != null ? bodyRb.position.y : transform.position.y;
            float height = y - Bird.StanceRootY(owner);
            if (height >= skipProbeAboveHeight && !nearPerch)
            {
                leftFootGrounded = false;
                rightFootGrounded = false;
            }
            else
            {
                Collider2D probe = perchCollider != null && perchCollider.enabled
                    ? perchCollider
                    : bodyCollider;
                bool grounded = IsFootGrounded(probe);
                leftFootGrounded = grounded;
                rightFootGrounded = grounded;
            }
        }
        else
        {
            leftFootGrounded = IsFootGrounded(leftFootCollider);
            rightFootGrounded = IsFootGrounded(rightFootCollider);
        }
        bothGrounded = leftFootGrounded && rightFootGrounded;
        // NearPerch is the whole crown AABB — dirt under the stump is inside it.
        // A slot is higher than dirt stance (~0); 0.35 m splits twig from soil.
        // Hunt dirt is soil even under the apple: leftover treeSupport
        // made OnDirtPlate false and BeakStrike never Rel Hostile.
        float supportY = bodyRb != null ? bodyRb.position.y : transform.position.y;
        treeSupport = !huntDirt
            && bothGrounded
            && (supportY - Bird.StanceRootY(owner)) > 0.35f
            && nearPerch;
        comOffsetX = ComputeComOffsetX();
    }

    public Vector2 GetCenterOfMass()
    {
        return comPosition;
    }

    private Vector2 ComputeCom()
    {
        if (bodies.Count == 0) return transform.position;

        Vector2 sum = Vector2.zero;
        float mass = 0f;
        for (int i = 0; i < bodies.Count; i++)
        {
            Rigidbody2D rb = bodies[i];
            if (rb == null) continue;
            sum += rb.position * rb.mass;
            mass += rb.mass;
        }
        if (mass < 0.001f) return transform.position;
        return sum / mass;
    }

    private float ComputeComOffsetX()
    {
        bool hasSupport = false;
        float minX = 0f;
        float maxX = 0f;
        Expand(leftFootCollider, leftFootGrounded, ref hasSupport, ref minX, ref maxX);
        Expand(rightFootCollider, rightFootGrounded, ref hasSupport, ref minX, ref maxX);
        if (!hasSupport) return 0f;
        return comPosition.x - (minX + maxX) * 0.5f;
    }

    private static void Expand(Collider2D col, bool grounded,
                               ref bool hasSupport, ref float minX, ref float maxX)
    {
        if (!grounded || col == null) return;
        Bounds b = col.bounds;
        if (!hasSupport)
        {
            minX = b.min.x;
            maxX = b.max.x;
            hasSupport = true;
        }
        else
        {
            if (b.min.x < minX) minX = b.min.x;
            if (b.max.x > maxX) maxX = b.max.x;
        }
    }

    private bool IsFootGrounded(Collider2D footCollider)
    {
        if (footCollider == null) return false;

        Bounds bounds = footCollider.bounds;
        Vector2 probeCenter = new Vector2(bounds.center.x, bounds.min.y);
        Vector2 probeSize = new Vector2(bounds.size.x * 0.9f, groundProbeThickness);
        int hitCount = Physics2D.OverlapBoxNonAlloc(
            probeCenter, probeSize, 0f, groundProbeHits, GroundLayers.Mask);
        probeCallCount++;
        probeHitSum += hitCount;
        if (hitCount >= groundProbeHits.Length)
            probeSaturatedCount++;
        return hitCount > 0;
    }
}
