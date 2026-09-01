using System.Collections.Generic;
using UnityEngine;

// Сенсорный слой птицы: только читает. Сил нет.
// Не использует VestibularSystem / CenterOfMassCalculator человека —
// те ищут Torso и Pelvis по имени и на птице молча отдают нули.
[DefaultExecutionOrder(-100)]
public class BirdSensors : MonoBehaviour
{
    [Header("Проба контакта стопы")]
    public float groundProbeThickness = 0.04f;
    [Tooltip("Выше этого над стойкой flock не зовёт OverlapBox: в крейсере проба всегда пустая.")]
    public float skipProbeAboveHeight = 0.5f;

    [Header("Центр масс")]
    public Vector2 comPosition;
    public Vector2 comVelocity;

    [Header("Тело")]
    [Tooltip("Тангаж тела, градусы. 0 — корпус горизонтален, плюс — нос вверх.")]
    public float bodyPitch;
    public float bodyPitchRate;
    public Vector2 bodyVelocity;
    public float neckTilt;
    public float neckTiltRate;
    public float headTilt;
    public float headTiltRate;

    [Header("Стопы")]
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
    private readonly Collider2D[] groundProbeHits = new Collider2D[8];
    private Vector2 previousCom;
    private bool hasPreviousCom;

    // Линейка цикла 1: сколько проб и попаданий за прогон.
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

        bodyCollider = body != null ? body.GetComponent<Collider2D>() : null;
        owner = GetComponent<Bird>();
        perchCollider = owner != null ? owner.perchCollider : null;

        Transform leftFoot = transform.Find("LeftLegFoot");
        Transform rightFoot = transform.Find("RightLegFoot");
        leftFootCollider = leftFoot != null ? leftFoot.GetComponent<Collider2D>() : null;
        rightFootCollider = rightFoot != null ? rightFoot.GetComponent<Collider2D>() : null;

        hasPreviousCom = false;
    }

    void FixedUpdate()
    {
        if (drivenExternally) return;
        Tick();
    }

    public void Tick()
    {
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
            // DeltaAngle: при кульбите тангаж уходит за ±90°, вычитание ломается.
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
        nearPerch = PlantTree.NearPerch(supportPos, 0.9f);

        if (leftFootCollider == null && rightFootCollider == null)
        {
            float y = bodyRb != null ? bodyRb.transform.position.y : transform.position.y;
            float height = y - Bird.StanceRootY(owner);
            // Cruise skip is for flat ground. A tree perch sits higher:
            // without this the bird never reads Sit on a branch.
            if (height >= skipProbeAboveHeight && !nearPerch)
            {
                leftFootGrounded = false;
                rightFootGrounded = false;
            }
            else
            {
                bool grounded = IsFootGrounded(perchCollider != null && perchCollider.enabled ? perchCollider : bodyCollider);
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
        float supportY = bodyRb != null ? bodyRb.position.y : transform.position.y;
        treeSupport = bothGrounded
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
