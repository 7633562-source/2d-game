using UnityEngine;

// Prey body, not a ragdoll. Faction Insects. A bird eats it with the beak.
public class Insect : MonoBehaviour
{
    public const float GroundClearance = 0.04f;
    // BirdDrive / flock enter Peck inside this X gap. Body HuntBeak far
    // edge is PeckHuntReach (PeckReach + insect box 0.08 m) so the first
    // Peck sits inside the trigger, not on the AABB edge. Do not raise
    // PeckReach to 0.48 — Rel Hostile would miss.
    public const float PeckReach = 0.40f;
    public const float PeckHuntReach = 0.48f;
    // Keep Peck after walkThrust overshoot. Flipping facing here yanks the
    // beak slab off the insect (body -0.10..+0.32 m along the old nose).
    public const float PeckHold = 0.55f;
    // Hunt Walk/Peck below this height over stance. The brain ticks in
    // Update before the first foot probe, so bothGrounded starts false
    // and used to write Fly instead of eating on dirt.
    public const float DirtHuntHeight = 0.45f;

    public static bool InPeckWindow(float dist, bool alreadyPecking)
    {
        // First Peck sits inside HuntBeak (PeckHuntReach 0.48), not on
        // the AABB edge. PeckReach 0.40 is the solid Rel core; a leftover
        // vx step can skip that 8 cm band and never arm the slab. Do not
        // raise PeckReach itself — Rel Hostile is still the mouth.
        if (dist < PeckHuntReach)
            return true;
        return alreadyPecking && dist < PeckHold;
    }

    // v1 look: cone along facing. Not a Vision component.
    // Card: Forage + sees Insects → Pursue. Glance turns first.
    // HuntBeak is body-X ±PeckHold, so a nose-away overlap is already
    // closing — that is not "behind the back" at range.
    public static bool MayPursue(
        float hunterX,
        float facing,
        float insectX,
        float noticeRange,
        bool alreadyPursuing)
    {
        float dist = Mathf.Abs(insectX - hunterX);
        if (dist >= noticeRange)
            return false;
        if (alreadyPursuing)
            return true;
        // Close overlap first: the old behind-check refused a PeckHold
        // sit with the nose the other way, so ChooseWake stayed Forage
        // and HuntBeak never Rel Hostile.
        if (InPeckWindow(dist, false))
            return true;
        float dx;
        return OrganismAlert.SeesAhead(
            hunterX, facing, insectX, noticeRange, 0.12f, out dx);
    }

    // Bound quarry glance. Forage outside noticeRange keeps wander
    // facing. Pursue keeps closing after a leftover cruise walks past
    // that leash — Sit/Forage there re-armed leftover Fly. Inside
    // PeckHold keep the current nose: leftover SetFacing reversed
    // walkThrust and walked the body-X HuntBeak off Rel Hostile.
    public static bool WantHuntFace(
        float hunterX,
        float insectX,
        float noticeRange,
        float currentFace,
        out float face)
    {
        return WantHuntFace(
            hunterX, insectX, noticeRange, currentFace, false, out face);
    }

    public static bool WantHuntFace(
        float hunterX,
        float insectX,
        float noticeRange,
        float currentFace,
        bool keepClosing,
        out float face)
    {
        float dx = insectX - hunterX;
        float dist = Mathf.Abs(dx);
        face = currentFace >= 0f ? 1f : -1f;
        if (dist >= noticeRange && !keepClosing)
            return false;
        // Inside PeckHold the HuntBeak strip already covers the insect
        // (body-X, both ways). A glance here — first Peck still last-mode
        // Walk, or walkThrust overshoot — flipped the nose. The AABB no
        // longer follows facing, but vx along the new face still walks
        // the body (and the slab) off Rel Hostile. Close at noticeRange.
        if (dist < PeckHold)
            return false;
        float want = dx > 0f ? 1f : -1f;
        if (face == want)
            return false;
        face = want;
        return true;
    }

    // Face while closing. HuntBeak is body-X ±PeckHold; leftover
    // SetFacing no longer teleports the AABB, but walkThrust still
    // uses the nose and walks the slab off Rel Hostile. holdingPeck
    // is last Peck OR this eat frame (first Peck still last-mode Walk).
    // Inside the strip keep the approach heading.
    public static bool WantCloseFace(float dx, bool holdingPeck, float currentFace)
    {
        if (holdingPeck)
            return false;
        if (Mathf.Abs(dx) <= PeckHold)
            return false;
        float want = dx > 0f ? 1f : -1f;
        float face = currentFace >= 0f ? 1f : -1f;
        return face != want;
    }

    // Perch is a twig: leave it. Dirt is grounded, or still near stance
    // while the probe has not ticked / missed one frame. Not a lift.
    // Dirt-height wins over onPerch: leftover pad / NearPerch AABB after
    // preferDirtLand flicker used to park Forage→Pursue Peck (HuntBeak
    // stayed Walk-parked, Rel Hostile never Hurt). Mirrors body
    // IsHuntSoil / HuntBodyNearDirt and brain HuntMustLeavePerch
    // (IsHuntSoil, not preferDirtLand alone). Woodpecker pad drum sits higher.
    public static bool OnDirtPlate(bool grounded, bool onPerch, float heightAboveStance)
    {
        if (heightAboveStance < DirtHuntHeight)
            return true;
        if (onPerch)
            return false;
        return grounded;
    }

    [Header("Life")]
    public float ownStrength = 0.2f;
    public Vector2 size = new Vector2(0.08f, 0.05f);

    private Damageable health;
    private FactionMember member;
    private BoxCollider2D huntBox;
    // Kinematic trigger body. Not a ragdoll: FreezeAll, no joints.
    // Unity 6 ANDs Collider2D + Rigidbody2D include/exclude masks; a
    // static trigger with includeLayers 0 dropped HuntBeak
    // Default×Default. layerOverridePriority lives on Collider2D only.
    private Rigidbody2D queryBody;

    public Damageable Life => health;
    public FactionMember Member => member;

    // Hunt cache. Call when the bound quarry changes, not every Update.
    // Missing stamp is still faction 3 — otherwise the brain writes leftover Fly.
    public static void BindHuntRefs(
        Transform quarry,
        out Insect insect,
        out FactionMember faction,
        out Damageable life)
    {
        insect = null;
        faction = null;
        life = null;
        if (quarry == null)
            return;
        insect = quarry.GetComponent<Insect>();
        if (insect != null)
        {
            if (insect.member == null
                || insect.member.faction != FactionId.Insects)
                insect.member = FactionStamp.Insect(quarry.gameObject);
            if (insect.health == null)
            {
                insect.health = quarry.GetComponent<Damageable>();
                if (insect.health == null)
                    insect.health = quarry.gameObject.AddComponent<Damageable>();
            }

            insect.LatchHuntQueryLayers();
            faction = insect.member;
            life = insect.health;
            return;
        }

        faction = quarry.GetComponent<FactionMember>();
        life = quarry.GetComponent<Damageable>();
    }

    // Stamp + flat box. No PNG, no Texture2D mark, no ragdoll.
    public static Transform SpawnAt(Vector2 position)
    {
        GameObject go = new GameObject("Insect");
        go.transform.position = position;
        // Stamp faction 3 before Insect.Awake so BindPrey in the same
        // Start already sees Rel Hostile (BeakStrike is the mouth).
        FactionStamp.Insect(go);
        go.AddComponent<Insect>();
        return go.transform;
    }

    public static Transform SpawnOnDirt(float x, float groundY)
    {
        return SpawnAt(new Vector2(x, groundY + GroundClearance));
    }

    void Awake()
    {
        member = FactionStamp.Insect(gameObject);
        health = GetComponent<Damageable>();
        if (health == null)
            health = gameObject.AddComponent<Damageable>();
        // One Peck (crow 0.5, hen 0.35) eats. Brain does not call Hurt.
        health.maxHealth = 0.3f;
        health.health = 0.3f;

        huntBox = GetComponent<BoxCollider2D>();
        if (huntBox == null)
            huntBox = gameObject.AddComponent<BoxCollider2D>();
        huntBox.size = size;
        huntBox.isTrigger = true;
        LatchHuntQueryLayers();

        SpriteRenderer sprite = GetComponent<SpriteRenderer>();
        if (sprite == null)
            sprite = gameObject.AddComponent<SpriteRenderer>();
        sprite.sprite = LevelArt.White();
        sprite.drawMode = SpriteDrawMode.Sliced;
        sprite.size = size;
        sprite.color = new Color(0.80f, 0.55f, 0.10f, 1f);
        sprite.sortingOrder = 6;
        SpriteLighting.ApplyLit(sprite);
    }

    // HuntBeak query is Default only (mask 1). Unity 6 ANDs both
    // colliders and both rigidbodies; includeLayers 0 (Nothing) plus
    // no Insect RB used the matrix and dropped Default×Default, so
    // BeakStrike OverlapCollider never saw Rel Hostile. Same Default
    // belt as HuntBeak. Collider priority 1; Rigidbody2D has no
    // layerOverridePriority (CS1061). HuntBeak (body) is a live
    // Dynamic solid while Peck/Attack; this Insect stays a FreezeAll
    // kinematic trigger. That pair is what OverlapCollider Rel
    // Hostile can see. Latch on spawn / bind — not in Update.
    private void LatchHuntQueryLayers()
    {
        const int defaultMask = 1;
        if (gameObject.layer != 0)
            gameObject.layer = 0;

        if (huntBox == null)
            huntBox = GetComponent<BoxCollider2D>();
        if (huntBox != null)
        {
            if (huntBox.includeLayers.value != defaultMask)
                huntBox.includeLayers = defaultMask;
            if (huntBox.excludeLayers.value != 0)
                huntBox.excludeLayers = 0;
            if (huntBox.layerOverridePriority < 1)
                huntBox.layerOverridePriority = 1;
        }

        if (queryBody == null)
            queryBody = GetComponent<Rigidbody2D>();
        if (queryBody == null)
            queryBody = gameObject.AddComponent<Rigidbody2D>();
        queryBody.bodyType = RigidbodyType2D.Kinematic;
        queryBody.simulated = true;
        queryBody.gravityScale = 0f;
        queryBody.constraints = RigidbodyConstraints2D.FreezeAll;
        queryBody.collisionDetectionMode = CollisionDetectionMode2D.Discrete;
        queryBody.interpolation = RigidbodyInterpolation2D.None;
        queryBody.useFullKinematicContacts = true;
        // HuntBeak is Dynamic while Peck. A sleeping FreezeAll
        // kinematic left the insect out of the Box2D pair, so
        // BeakStrike OverlapCollider never saw Rel Hostile. Stay
        // awake; Rel is the eat gate.
        queryBody.sleepMode = RigidbodySleepMode2D.NeverSleep;
        if (queryBody.includeLayers.value != defaultMask)
            queryBody.includeLayers = defaultMask;
        if (queryBody.excludeLayers.value != 0)
            queryBody.excludeLayers = 0;
        if (queryBody.IsSleeping())
            queryBody.WakeUp();
    }

    void Update()
    {
        if (health != null && health.IsDead)
            Destroy(gameObject);
    }
}
