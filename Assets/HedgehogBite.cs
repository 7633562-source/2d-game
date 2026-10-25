using UnityEngine;

// The jaw segment is the only hedgehog hit instrument. Tick in Update.
// Stance arms the bite; this reads contacts and calls Hurt on berries.
// Not a brain: it does not write intent and does not AddForce.
[DefaultExecutionOrder(20)]
public class HedgehogBite : MonoBehaviour
{
    public float damage = 1f;
    public float cooldown = 0.40f;
    [Tooltip("Snout past the jaw box, m. Berries sit on the bush, not in the teeth.")]
    public float reachForward = 0.08f;
    [Tooltip("Up from the jaw box, m. Fruit hangs a few centimetres above the snout.")]
    public float reachUp = 0.06f;

    public int HitCount { get; private set; }

    private BoxCollider2D jaw;
    private HedgehogStanceController stance;
    private Damageable self;
    private readonly Collider2D[] overlap = new Collider2D[8];
    private float nextHit;

    public void Bind(BoxCollider2D jawCollider, HedgehogStanceController mouth)
    {
        jaw = jawCollider;
        stance = mouth;
    }

    void Awake()
    {
        self = GetComponent<Damageable>();
        if (stance == null)
            stance = GetComponent<HedgehogStanceController>();
    }

    void Update()
    {
        if (jaw == null || stance == null || !jaw.enabled)
            return;
        if (stance.energyRest)
            return;
        if (!stance.jawStrikeArmed)
            return;
        if (Time.time < nextHit)
            return;

        Vector2 size = jaw.bounds.size;
        size.x += Mathf.Max(0f, reachForward);
        size.y += Mathf.Max(0f, reachUp);
        Vector2 center = jaw.bounds.center;
        center += (Vector2)jaw.transform.right * (reachForward * 0.5f);
        center += Vector2.up * (reachUp * 0.5f);
        float angle = jaw.transform.eulerAngles.z;
        int n = Physics2D.OverlapBoxNonAlloc(center, size, angle, overlap);
        for (int i = 0; i < n; i++)
        {
            Collider2D other = overlap[i];
            if (other == null || other == jaw)
                continue;
            StrawberryBerry fruit = other.GetComponentInParent<StrawberryBerry>();
            if (fruit == null || !fruit.edible)
                continue;
            Damageable victim = fruit.Life;
            if (victim == null || victim == self || victim.IsDead)
                continue;

            victim.Hurt(damage, transform);
            HitCount++;
            nextHit = Time.time + cooldown;
            return;
        }
    }
}
