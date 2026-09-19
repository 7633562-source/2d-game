using System;
using System.Collections.Generic;
using UnityEngine;

// ─── BALANCE-ARENA-1: THE THROWER ───
//
// Yury's order 19.09.2026: a scene where the human stands and objects are thrown at
// different parts of his body, so his balance is measured against a known impulse.
//
// Contract (Assets/../.cursor/tasks/BALANCE-ARENA-1.md):
//   * ONE real Rigidbody2D per throw, with a real CircleCollider2D or BoxCollider2D
//     of the requested size. Mass and shape are INDEPENDENT parameters.
//   * The object is never a scripted force on the human: no code here touches a
//     human joint, muscle or rigidbody. The only thing that reaches the human is
//     the solver's own collision response.
//   * STRICTLY 2D: the flight is horizontal, inside the X-Y plane; gravityScale 0
//     keeps the trajectory exactly on the aimed line instead of inventing a fake
//     arc the aim switch could not express.
//   * The aim / timing / side jitter comes from the seed, never from frame timing.
//
// The object is a Rigidbody2D of the requested SHAPE - nothing here is a "ball" or
// a "sword": circle, square and rect are the three legal 2D bodies of the card.
public class ImpactThrower : MonoBehaviour
{
    public enum ShapeKind
    {
        Circle = 0,
        Square = 1,
        Rect = 2
    }

    [Header("The object: mass and shape are INDEPENDENT")]
    public float massKg = 4f;
    public string shapeText = "circle";
    public float sizeMeters = 0.25f;
    // rect only: the length along the flight axis is sizeMeters, the height is
    // sizeMeters / rectAspect.
    public float rectAspect = 2f;

    [Header("The throw")]
    public float speedMps = 8f;
    // head | torso | shoulder | thigh | shin - the body-part family of the card.
    public string aimPart = "torso";
    // left | right | seed. left = thrown from the left, right = from the right,
    // seed = the seed picks per throw (L2: time, side and aim must vary with it).
    public string sideText = "seed";
    public float firstThrowAt = 8f;
    public int throwCount = 3;
    public float spacingSeconds = 6f;
    public int seed = 1;

    [Header("Geometry")]
    // Horizontal stand-off between the launch point and the aimed part.
    public float spawnDistance = 2.6f;
    // 0 = straight line in the X-Y plane (the aim switch IS the contact height).
    public float projectileGravity = 0f;
    public float projectileLifetime = 5.5f;
    public float aimJitterMeters = 0.04f;
    public float timeJitterSeconds = 0.25f;
    public float speedJitterFraction = 0.03f;

    [Header("Placeholder colours (this scene is about force, not looks)")]
    public Color colorCircle = new Color(0.85f, 0.35f, 0.15f, 1f);
    public Color colorSquare = new Color(0.25f, 0.45f, 0.85f, 1f);
    public Color colorRect = new Color(0.55f, 0.25f, 0.70f, 1f);
    public int spriteSortingOrder = 5;

    // One planned throw. Mass and speed are the ORDERED values; the jitter fields
    // are the seed's contribution to aim and timing only.
    public struct ThrowPlan
    {
        public int index;
        public float time;          // scheduled time, jittered by the seed
        public float mass;
        public float speed;
        public float speedJitter;
        public float width;         // collider size, metres
        public float height;
        public string shape;
        public string part;         // the exact human segment to aim at
        public int side;            // -1 = from the left, +1 = from the right
        public float jitterX;
        public float jitterY;
        public float impulseNs;     // OUR reference: ordered mass * ordered speed
        public bool fired;          // latched when the body was spawned
    }

    private readonly List<ThrowPlan> plan = new List<ThrowPlan>(16);
    private readonly List<ImpactProjectile> live = new List<ImpactProjectile>(8);
    private Human human;
    private bool configured;

    // The simulation time of the NEXT fixed step. Collision callbacks are delivered
    // inside the step they belong to, i.e. before the harness calls Advance() again,
    // so a projectile that stamps this value stamps the time of the frame the
    // contact really happened in - to the frame, not to the callback's wall clock.
    public float NextStepTime { get; private set; }
    public int PlannedCount => plan.Count;
    public int FiredCount { get; private set; }

    public ThrowPlan GetPlan(int index)
    {
        return index >= 0 && index < plan.Count ? plan[index] : default(ThrowPlan);
    }

    public void Configure(Human target)
    {
        human = target;
        plan.Clear();
        live.Clear();
        FiredCount = 0;
        configured = false;
        BuildPlan();
        configured = true;
        NextStepTime = firstThrowAt;
    }

    public void ClearProjectiles()
    {
        for (int i = 0; i < live.Count; i++)
        {
            if (live[i] != null)
                Destroy(live[i].gameObject);
        }
        live.Clear();
    }

    // Called once per simulated fixed step by the scene or the trial harness.
    public void Advance(float time)
    {
        if (!configured) return;
        NextStepTime = time + Time.fixedDeltaTime;

        for (int i = 0; i < plan.Count; i++)
        {
            ThrowPlan p = plan[i];
            if (p.fired || p.time > time) continue;
            p.fired = true;
            plan[i] = p;
            Fire(p, time + Time.fixedDeltaTime);
        }

        for (int i = live.Count - 1; i >= 0; i--)
        {
            ImpactProjectile projectile = live[i];
            if (projectile == null)
            {
                live.RemoveAt(i);
                continue;
            }
            projectile.StepTime = NextStepTime;
            if (projectile.LifetimeExpired(time))
            {
                Destroy(projectile.gameObject);
                live.RemoveAt(i);
            }
        }
    }

    public ImpactProjectile ProjectileFor(int throwIndex)
    {
        for (int i = 0; i < live.Count; i++)
        {
            if (live[i] != null && live[i].ThrowIndex == throwIndex)
                return live[i];
        }
        return null;
    }

    public Collider2D ColliderFor(int throwIndex)
    {
        ImpactProjectile projectile = ProjectileFor(throwIndex);
        return projectile != null ? projectile.BodyCollider : null;
    }

    private bool HasProjectile(int throwIndex)
    {
        return ProjectileFor(throwIndex) != null;
    }

    // ─── THE PLAN ───
    // Everything the seed decides is drawn here, once, in a fixed order, so two runs
    // with the same -impactSeed throw the same objects at the same parts at the same
    // times. Nothing here reads a clock, a frame or a random source of the engine.
    private void BuildPlan()
    {
        System.Random rng = new System.Random(seed);
        for (int i = 0; i < Mathf.Max(0, throwCount); i++)
        {
            ThrowPlan p = new ThrowPlan();
            p.index = i;
            p.mass = massKg;
            p.speed = speedMps;
            p.shape = NormaliseShape(shapeText);
            p.width = sizeMeters;
            p.height = p.shape == "circle" || p.shape == "square"
                ? sizeMeters
                : sizeMeters / Mathf.Max(1.05f, rectAspect);
            p.speedJitter = 1f + (float)(rng.NextDouble() * 2.0 - 1.0) * speedJitterFraction;
            p.time = firstThrowAt + i * spacingSeconds
                + (float)(rng.NextDouble() * 2.0 - 1.0) * timeJitterSeconds;
            if (p.time < 0.2f) p.time = 0.2f;
            p.part = PickPart(rng);
            p.side = PickSide(rng);
            p.jitterX = (float)(rng.NextDouble() * 2.0 - 1.0) * aimJitterMeters;
            p.jitterY = (float)(rng.NextDouble() * 2.0 - 1.0) * aimJitterMeters;
            // The reference impulse is OUR number in the log: ordered mass * ordered
            // speed, never an input to the human's controller.
            p.impulseNs = p.mass * p.speed;
            plan.Add(p);
        }
    }

    private string NormaliseShape(string raw)
    {
        string shape = (raw ?? string.Empty).Trim().ToLowerInvariant();
        if (shape == "square" || shape == "rect" || shape == "rectangle") 
            return shape == "rectangle" ? "rect" : shape;
        return "circle";
    }

    private string PickPart(System.Random rng)
    {
        string family = (aimPart ?? "torso").Trim().ToLowerInvariant();
        string[] parts;
        switch (family)
        {
            case "head": parts = new[] { "Head" }; break;
            case "shoulder": parts = new[] { "LeftArmUpper", "RightArmUpper" }; break;
            case "thigh": parts = new[] { "LeftLegThigh", "RightLegThigh" }; break;
            case "shin": parts = new[] { "LeftLegShin", "RightLegShin" }; break;
            case "torso": parts = new[] { "Torso", "Pelvis" }; break;
            default: parts = new[] { "Torso" }; break;
        }
        int index = parts.Length == 1 ? 0 : (rng.NextDouble() < 0.5 ? 0 : 1);
        return parts[index];
    }

    private int PickSide(System.Random rng)
    {
        string side = (sideText ?? "seed").Trim().ToLowerInvariant();
        if (side == "left") return -1;
        if (side == "right") return 1;
        return rng.NextDouble() < 0.5 ? -1 : 1;
    }

    // ─── THE THROW ───
    private void Fire(ThrowPlan p, float stepTime)
    {
        Transform part = human != null ? human.transform.Find(p.part) : null;
        Vector2 aim = part != null
            ? (Vector2)part.position + new Vector2(p.jitterX, p.jitterY)
            : new Vector2(0f, -0.9f);
        Vector2 launch = new Vector2(aim.x - p.side * spawnDistance, aim.y);
        float speed = p.speed * p.speedJitter;
        Vector2 velocity = new Vector2(p.side * speed, 0f);

        GameObject go = new GameObject("Impact_" + p.index + "_" + p.shape);
        go.transform.position = new Vector3(launch.x, launch.y, 0f);

        Rigidbody2D body = go.AddComponent<Rigidbody2D>();
        body.mass = p.mass;
        body.gravityScale = projectileGravity;
        body.linearDamping = 0f;
        body.angularDamping = 0.05f;
        // 4-20 m/s through a 0.1 m limb: without continuous detection the solver can
        // step over the collider and the "impact" would never happen.
        body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        body.interpolation = RigidbodyInterpolation2D.None;
        body.linearVelocity = velocity;

        // A real collider of the requested shape and size. An explicit friction
        // material, never the engine default (project rule, collider-materials).
        PhysicsMaterial2D material = new PhysicsMaterial2D("ImpactObject");
        material.friction = 0.4f;
        material.bounciness = 0f;
        CircleCollider2D circle = null;
        BoxCollider2D box = null;
        if (p.shape == "circle")
        {
            circle = go.AddComponent<CircleCollider2D>();
            circle.radius = Mathf.Max(0.02f, p.width * 0.5f);
            circle.sharedMaterial = material;
        }
        else
        {
            box = go.AddComponent<BoxCollider2D>();
            box.size = new Vector2(Mathf.Max(0.04f, p.width), Mathf.Max(0.04f, p.height));
            box.sharedMaterial = material;
        }

        SpriteRenderer renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = p.shape == "circle" ? DiscSprite() : SquareSprite();
        renderer.color = p.shape == "circle" ? colorCircle
            : p.shape == "square" ? colorSquare : colorRect;
        renderer.drawMode = SpriteDrawMode.Sliced;
        renderer.size = new Vector2(Mathf.Max(0.04f, p.width), Mathf.Max(0.04f, p.height));
        renderer.sortingOrder = spriteSortingOrder;

        ImpactProjectile projectile = go.AddComponent<ImpactProjectile>();
        projectile.Configure(this, p.index, p.shape, p.width, p.height, p.mass,
            speed, p.impulseNs, p.part, p.side, launch, aim, velocity, projectileLifetime,
            stepTime, body.gravityScale);
        live.Add(projectile);
        FiredCount++;
    }

    private Sprite squareSprite;
    private Sprite discSprite;

    private Sprite SquareSprite()
    {
        if (squareSprite != null) return squareSprite;
        Texture2D tex = new Texture2D(8, 8, TextureFormat.RGBA32, false);
        Color32 opaque = new Color32(255, 255, 255, 255);
        for (int y = 0; y < 8; y++)
            for (int x = 0; x < 8; x++)
                tex.SetPixel(x, y, opaque);
        tex.Apply(false, false);
        // No 9-slice border: a border plus Sliced is what throws on a small body.
        squareSprite = Sprite.Create(tex, new Rect(0f, 0f, 8f, 8f), new Vector2(0.5f, 0.5f), 8f);
        return squareSprite;
    }

    private Sprite DiscSprite()
    {
        if (discSprite != null) return discSprite;
        const int size = 32;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        float radius = size * 0.5f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x + 0.5f - radius;
                float dy = y + 0.5f - radius;
                float distance = Mathf.Sqrt(dx * dx + dy * dy);
                float alpha = Mathf.Clamp01(radius - distance);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }
        tex.Apply(false, false);
        discSprite = Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f),
            size);
        return discSprite;
    }
}

// ─── THE THROWN BODY ───
// One per throw. It carries its own ordered parameters, the launch data and the
// MEASURED contact facts: the first frame it touched the human, which segment it
// touched, and the impulse the solver actually delivered through that contact.
public class ImpactProjectile : MonoBehaviour
{
    public int ThrowIndex { get; private set; }
    public string ShapeText { get; private set; }
    public float Width { get; private set; }
    public float Height { get; private set; }
    public float MassKg { get; private set; }
    public float SpeedMps { get; private set; }
    public float ReferenceImpulseNs { get; private set; }
    public string AimedPart { get; private set; }
    public int Side { get; private set; }
    public Vector2 LaunchPoint { get; private set; }
    public Vector2 AimPoint { get; private set; }
    public Vector2 LaunchVelocity { get; private set; }
    public float BornTime { get; private set; }
    public float GravityScale { get; private set; }
    public Rigidbody2D Body { get; private set; }
    public Collider2D BodyCollider { get; private set; }

    // The simulation time of the step whose physics is running right now.
    public float StepTime { get; set; }

    public bool ContactedHuman { get; private set; }
    public string ContactPart { get; private set; }
    public Vector2 ContactPoint { get; private set; }
    public float ContactTime { get; private set; }
    public float ClosingSpeedMps { get; private set; }
    // Solver impulses through the human contact, accumulated over enter + stay.
    public float DeliveredImpulseNs { get; private set; }
    public float DeliveredVerticalImpulseNs { get; private set; }
    public float DeliveredImpulseAtEntryNs { get; private set; }
    public float MaxAbsZ { get; private set; }
    public bool OutOfPlane { get; private set; }

    private float lifetimeSeconds;

    public void Configure(ImpactThrower owner, int throwIndex, string shape, float width,
        float height, float mass, float speed, float referenceImpulse, string aimedPart,
        int side, Vector2 launch, Vector2 aim, Vector2 velocity, float lifetime,
        float birthTime, float gravityScale)
    {
        ThrowIndex = throwIndex;
        ShapeText = shape;
        Width = width;
        Height = height;
        MassKg = mass;
        SpeedMps = speed;
        ReferenceImpulseNs = referenceImpulse;
        AimedPart = aimedPart;
        Side = side;
        LaunchPoint = launch;
        AimPoint = aim;
        LaunchVelocity = velocity;
        lifetimeSeconds = lifetime;
        BornTime = birthTime;
        GravityScale = gravityScale;
        ContactTime = -1f;
        ContactPart = string.Empty;
        Body = GetComponent<Rigidbody2D>();
        BodyCollider = GetComponent<Collider2D>();
    }

    public bool LifetimeExpired(float time)
    {
        return time - BornTime >= lifetimeSeconds;
    }

    private void FixedUpdate()
    {
        if (transform.position.z != 0f)
        {
            OutOfPlane = true;
            MaxAbsZ = Mathf.Max(MaxAbsZ, Mathf.Abs(transform.position.z));
        }
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        Accumulate(collision, true);
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        Accumulate(collision, false);
    }

    // The contact is worth measuring only when the other collider belongs to the
    // human: the ground under him and any scenery are not the impact.
    private void Accumulate(Collision2D collision, bool enter)
    {
        if (collision == null || collision.collider == null) return;
        Human other = collision.collider.GetComponentInParent<Human>();
        if (other == null) return;

        float vertical = 0f;
        float total = 0f;
        int count = collision.contactCount;
        for (int i = 0; i < count; i++)
        {
            ContactPoint2D cp = collision.GetContact(i);
            float magnitude = cp.normalImpulse;
            total += magnitude;
            vertical += magnitude * cp.normal.y;
        }

        if (enter && !ContactedHuman)
        {
            ContactedHuman = true;
            ContactTime = StepTime;
            ContactPart = collision.collider.transform.name;
            ContactPoint = collision.GetContact(0).point;
            ClosingSpeedMps = collision.relativeVelocity.magnitude;
            DeliveredImpulseAtEntryNs = total;
        }
        DeliveredImpulseNs += total;
        DeliveredVerticalImpulseNs += vertical;
    }
}
