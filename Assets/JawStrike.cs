using UnityEngine;

// The jaw segment is the only dog hit instrument. Tick in Update, not 200 Hz.
// DogStanceController arms the bite; this reads contacts and calls Hurt.
// Not a brain: it does not write intent and does not AddForce.
[DefaultExecutionOrder(20)]
public class JawStrike : MonoBehaviour
{
    public float damage = 1.2f;
    public float cooldown = 0.45f;

    public int HitCount { get; private set; }

    private BoxCollider2D jaw;
    private DogStanceController stance;
    private Damageable self;
    private readonly Collider2D[] overlap = new Collider2D[8];
    private float nextHit;

    public void Bind(BoxCollider2D jawCollider, DogStanceController mouth)
    {
        jaw = jawCollider;
        stance = mouth;
    }

    void Awake()
    {
        self = GetComponent<Damageable>();
        if (stance == null)
            stance = GetComponent<DogStanceController>();
    }

    void Update()
    {
        if (jaw == null || stance == null || !jaw.enabled)
            return;
        if (!stance.jawStrikeArmed)
            return;
        if (Time.time < nextHit)
            return;

        Vector2 center = jaw.bounds.center;
        Vector2 size = jaw.bounds.size;
        float angle = jaw.transform.eulerAngles.z;
        int n = Physics2D.OverlapBoxNonAlloc(center, size, angle, overlap);
        for (int i = 0; i < n; i++)
        {
            Collider2D other = overlap[i];
            if (other == null || other == jaw)
                continue;
            Damageable victim = other.GetComponentInParent<Damageable>();
            if (victim == null || victim == self)
                continue;
            if (victim.transform == transform)
                continue;

            victim.Hurt(damage, transform);
            HitCount++;
            nextHit = Time.time + cooldown;
            return;
        }
    }
}
