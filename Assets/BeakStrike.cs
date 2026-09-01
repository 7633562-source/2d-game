using UnityEngine;

// The beak segment is the only hit instrument. Tick in Update, not 200 Hz.
// Brain writes Peck / Attack; this reads contacts and calls Damageable.Hurt.
[DefaultExecutionOrder(20)]
public class BeakStrike : MonoBehaviour
{
    public float damage = 0.35f;
    public float cooldown = 0.4f;

    public int HitCount { get; private set; }

    private BoxCollider2D beak;
    private BirdController control;
    private Damageable self;
    private Bird selfBird;
    private readonly Collider2D[] overlap = new Collider2D[8];
    private float nextHit;

    public void Bind(BoxCollider2D beakCollider)
    {
        beak = beakCollider;
    }

    void Awake()
    {
        control = GetComponent<BirdController>();
        self = GetComponent<Damageable>();
        selfBird = GetComponent<Bird>();
    }

    void Update()
    {
        if (beak == null || control == null || !beak.enabled)
            return;
        if (control.mode != BirdMode.Peck && control.mode != BirdMode.Attack)
            return;
        if (Time.time < nextHit)
            return;

        Vector2 center = beak.bounds.center;
        Vector2 size = beak.bounds.size;
        float angle = beak.transform.eulerAngles.z;
        int n = Physics2D.OverlapBoxNonAlloc(center, size, angle, overlap);
        for (int i = 0; i < n; i++)
        {
            Collider2D other = overlap[i];
            if (other == null || other == beak)
                continue;
            Damageable victim = other.GetComponentInParent<Damageable>();
            if (victim == null || victim == self)
                continue;
            if (victim.transform == transform)
                continue;
            Bird otherBird = victim.GetComponent<Bird>();
            if (selfBird != null && otherBird != null && otherBird.kind == selfBird.kind)
                continue;

            victim.Hurt(damage, transform);
            HitCount++;
            nextHit = Time.time + cooldown;
            return;
        }
    }
}
