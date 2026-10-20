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
    private FactionMember selfFaction;
    private BirdDrive huntDrive;
    private bool insectEatArmed;
    // Ground + BirdLimb used to fill OverlapBox[8] on the 1.03 m dirt
    // strip before the Insect, so Rel Hostile never reached Hurt.
    private readonly Collider2D[] overlap = new Collider2D[16];
    private ContactFilter2D eatFilter;
    private bool eatFilterReady;
    private float nextHit;
    private Collider2D cachedHit;
    private Damageable cachedVictim;
    private FactionMember cachedVictimFaction;
    private Bird cachedVictimBird;
    private bool cachedVictimInsect;

    public void Bind(BoxCollider2D beakCollider)
    {
        beak = beakCollider;
    }

    // BuildBird adds this before BirdDrive. Awake is too early to cache
    // the drive; BirdDrive.Awake binds once. Flock has no per-bird drive
    // — BirdFlockDrive writes ArmInsectEat after ChooseWake (order −50).
    public void BindHunter(BirdDrive drive)
    {
        huntDrive = drive;
    }

    public void ArmInsectEat(bool armed)
    {
        insectEatArmed = armed;
    }

    void Awake()
    {
        control = GetComponent<BirdController>();
        self = GetComponent<Damageable>();
        selfBird = GetComponent<Bird>();
        selfFaction = GetComponent<FactionMember>();
    }

    void Update()
    {
        if (beak == null || control == null || !beak.enabled)
            return;
        if (control.energyRest)
            return;
        if (control.mode != BirdMode.Peck && control.mode != BirdMode.Attack)
            return;
        if (Time.time < nextHit)
            return;
        // Rel Hostile is the eat gate. Wildlife.None locked Rel Neutral
        // and BirdHuntsInsect failed; stamp Wildlife.Bird, not a one-shot.
        EnsureHuntFaction();
        EnsureEatFilter();

        // HuntBeak fixture (offset on dirt), not Transform.bounds/eulerZ.
        // Leftover Fly pitch on the Body Transform rotated OverlapBox
        // into Ground and filled 8 hits before the Insect. Ground and
        // BirdLimb stay out of the mask so Rel Hostile can reach Hurt.
        int n = beak.Overlap(eatFilter, overlap);
        for (int i = 0; i < n; i++)
        {
            Collider2D other = overlap[i];
            if (other == null || other == beak)
                continue;
            int layer = other.gameObject.layer;
            if (layer == GroundLayers.Index)
                continue;
            Damageable victim;
            FactionMember otherFaction;
            Bird otherBird;
            ResolveVictim(other, out victim, out otherFaction, out otherBird);
            if (victim == null || victim == self)
                continue;
            if (victim.transform == transform)
                continue;
            if (!MayHurt(otherFaction, otherBird))
                continue;

            victim.Hurt(damage, transform);
            HitCount++;
            nextHit = Time.time + cooldown;
            return;
        }
    }

    // Same collider: reuse. Destroyed victim is Unity-null — look up again.
    private void ResolveVictim(
        Collider2D other,
        out Damageable victim,
        out FactionMember otherFaction,
        out Bird otherBird)
    {
        if (ReferenceEquals(other, cachedHit) && cachedVictim != null)
        {
            victim = cachedVictim;
            otherFaction = cachedVictimFaction;
            otherBird = cachedVictimBird;
            return;
        }

        cachedHit = other;
        victim = other.GetComponentInParent<Damageable>();
        cachedVictim = victim;
        if (victim == null)
        {
            cachedVictimFaction = null;
            cachedVictimBird = null;
            cachedVictimInsect = false;
            otherFaction = null;
            otherBird = null;
            return;
        }

        Insect bug = victim.GetComponent<Insect>();
        cachedVictimInsect = bug != null;
        if (bug != null)
        {
            Insect.BindHuntRefs(victim.transform, out _, out otherFaction, out _);
            otherBird = null;
        }
        else
        {
            otherFaction = victim.GetComponent<FactionMember>();
            otherBird = victim.GetComponent<Bird>();
        }
        cachedVictimFaction = otherFaction;
        cachedVictimBird = otherBird;
    }

    private void EnsureHuntFaction()
    {
        if (selfFaction != null
            && selfFaction.faction == FactionId.Wildlife
            && selfFaction.subfaction == SubfactionId.Bird)
            return;
        selfFaction = FactionStamp.Bird(gameObject);
    }

    private void EnsureEatFilter()
    {
        if (eatFilterReady)
            return;
        eatFilter = new ContactFilter2D();
        eatFilter.useTriggers = true;
        // HuntBeak include is Default only (Insects). ~exclude used to
        // admit Water / UI / empty layers and fill overlap[16] before
        // the bug, so Rel Hostile never Hurt. Latch once — Default is
        // layer 0, no NameToLayer in Update.
        eatFilter.SetLayerMask(1);
        eatFilterReady = true;
    }

    private bool MayHurt(FactionMember otherFaction, Bird otherBird)
    {
        // Card: Forage + see Insects → Pursue → Peck → Hurt.
        // A stamp miss used to fall through to `return true` and eat
        // on a Forage dirt Peck (Rel never asked).
        bool insectVictim = cachedVictimInsect
            || FactionTable.BirdHuntsInsect(selfFaction, otherFaction)
            || (otherFaction != null && otherFaction.faction == FactionId.Insects);
        if (insectVictim)
        {
            // Stamp miss used to refuse Hurt while Life was already Pursue
            // (Rel Neutral for one overlap). Restamp both; Rel is the mouth.
            if (cachedVictim != null
                && (otherFaction == null || otherFaction.faction != FactionId.Insects))
            {
                Insect.BindHuntRefs(cachedVictim.transform, out _, out otherFaction, out _);
                cachedVictimFaction = otherFaction;
            }

            if (!FactionTable.BirdHuntsInsect(selfFaction, otherFaction))
            {
                // First overlap can still be Wildlife.None if Awake beat
                // FactionStamp.Bird. EnsureHuntFaction already ran; retry
                // after the insect restamp so Rel Hostile can Hurt.
                EnsureHuntFaction();
                if (!FactionTable.BirdHuntsInsect(selfFaction, otherFaction))
                    return false;
            }
            return InsectEatArmed();
        }

        // Hunt Peck is Insects only. A 1.03 m dirt strip must not Hurt
        // a berry / neighbour while Life is Pursue.
        if (InsectEatArmed())
            return false;
        if (selfFaction != null && otherFaction != null)
            return FactionTable.Rel(selfFaction, otherFaction) == FactionStance.Hostile;
        if (selfBird != null && otherBird != null && otherBird.kind == selfBird.kind)
            return false;
        return true;
    }

    // Card: Forage + see Insects → Pursue → Peck → Hurt.
    // Crow dirt-forage Peck sets preferDirtLand (DirtBout) and HuntBeak
    // still Rel Hostile — a neighbour / pebble Peck ate without Pursue.
    private bool InsectEatArmed()
    {
        if (huntDrive != null)
            return huntDrive.Life == LifeState.Pursue;
        return insectEatArmed;
    }
}
