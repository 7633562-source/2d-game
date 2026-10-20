using System.Collections.Generic;
using UnityEngine;

// THE BIRD'S-CROSSING PROBE (owner's landing sheet, 25.09).
//
// A probe body carries the bird's own body box and is flown along a path that
// weaves through the crown and crosses every point of the landing ring. The two
// counts come from the SOLVER, never from a list of names:
//
//   * solid contacts (OnCollisionEnter/Stay) — they must stay 0: the bird
//     geometry never intersects the tree;
//   * trigger overlaps (OnTriggerEnter/Stay) — the ring is what a bird can land
//     on, and the probe must find it.
//
// Before the flight the probe takes the same preparation a bird takes
// (Bird.GhostTreeWood): every tree collider that is not a trigger is ignored for
// it — and that preparation is then READ BACK with Physics2D.GetIgnoreCollision,
// so a solid collider that stayed un-ghosted is counted in `UnguardedSolid`
// instead of being quietly forgiven.
//
// PLANT-SIDE TEST BODY. It is not a bird, it drives nothing but itself, it
// touches no bird file, and the headless harness spawns it only with -treeProbe.
public class TreeLandingProbe : MonoBehaviour
{
    // The bird's own body box (Assets/Bird.cs:184, `bodySize`). The probe is a
    // rectangle for the same reason the simple scenes are: the physics is what
    // is being read.
    public const float BirdBodyWidthM = 0.14f;
    public const float BirdBodyHeightM = 0.06f;

    public float speed = 2f;
    public Vector2 bodySize = new Vector2(BirdBodyWidthM, BirdBodyHeightM);
    // How far below and above a mark the weave enters and leaves the wood.
    public float weaveHalfM = 0.18f;
    // No step of the weave is longer than this; the whole path must fit the run.
    public float pathSeconds = 6f;

    private Rigidbody2D rb;
    private readonly List<Vector2> path = new List<Vector2>(64);
    private readonly List<Collider2D> solidSeen = new List<Collider2D>(8);
    private readonly List<Collider2D> triggerSeen = new List<Collider2D>(8);
    private int cursor;

    public int SolidColliderHits { get; private set; }
    public int SolidContactFrames { get; private set; }
    public int TriggerHits { get; private set; }
    public int TriggerOverlapFrames { get; private set; }
    public int GuardedSolid { get; private set; }
    public int UnguardedSolid { get; private set; }
    public int TreeTriggerColliders { get; private set; }
    public int PathSteps { get; private set; }
    public float PathLengthM { get; private set; }
    public string FirstSolidName { get; private set; } = string.Empty;
    public int FirstSolidStep { get; private set; }
    public int FirstSolidStayStep { get; private set; }

    public static TreeLandingProbe Attach(PlantTree tree, float speed)
    {
        if (tree == null)
            return null;
        GameObject go = new GameObject("TreeLandingProbe");
        go.transform.position = tree.transform.position;
        TreeLandingProbe probe = go.AddComponent<TreeLandingProbe>();
        probe.speed = speed;
        probe.Build(tree);
        return probe;
    }

    private void Build(PlantTree tree)
    {
        rb = gameObject.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Dynamic;
        rb.gravityScale = 0f;
        rb.mass = 0.05f;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        // The bird's own contact layer, so the probe meets exactly what a bird
        // meets and nothing else.
        BirdContactLayers.ApplyCore(gameObject);
        BoxCollider2D box = gameObject.AddComponent<BoxCollider2D>();
        box.size = bodySize;
        Prepare(tree);
        BuildPath(tree);
        // Spawn ON the first waypoint: a probe born at the tree's base would
        // start inside the ground it is not read about and report a contact
        // that says nothing about the tree.
        if (path.Count > 0)
            rb.position = path[0];
    }

    private void Prepare(PlantTree tree)
    {
        Collider2D mine = GetComponent<Collider2D>();
        if (mine == null)
            return;
        List<Collider2D> cols = new List<Collider2D>(128);
        tree.GetComponentsInChildren(true, cols);
        for (int i = 0; i < cols.Count; i++)
        {
            Collider2D c = cols[i];
            if (c == null)
                continue;
            if (c.isTrigger)
            {
                // The landing ring (and any BirdPerch pad) stays live: these are
                // the only places a bird may land, and the probe must meet them.
                TreeTriggerColliders++;
                continue;
            }
            Physics2D.IgnoreCollision(mine, c, true);
            if (Physics2D.GetIgnoreCollision(mine, c))
                GuardedSolid++;
            else
                UnguardedSolid++;
        }
    }

    private void BuildPath(PlantTree tree)
    {
        Vector2[] slots = tree.GetLandingSlots();
        List<Vector2> pts = new List<Vector2>(slots.Length);
        for (int i = 0; i < slots.Length; i++)
            pts.Add(slots[i]);
        // Highest mark first, then outward, so the weave comes down through the
        // crown instead of jumping across it.
        pts.Sort((a, b) =>
        {
            int byY = b.y.CompareTo(a.y);
            if (byY != 0) return byY;
            return a.x.CompareTo(b.x);
        });

        path.Clear();
        for (int i = 0; i < pts.Count; i++)
        {
            path.Add(pts[i] + new Vector2(0f, -weaveHalfM));
            path.Add(pts[i] + new Vector2(0f, weaveHalfM));
        }

        PathLengthM = 0f;
        for (int i = 1; i < path.Count; i++)
            PathLengthM += (path[i] - path[i - 1]).magnitude;
        if (PathLengthM > 0.01f && pathSeconds > 0.1f)
            speed = Mathf.Clamp(PathLengthM / pathSeconds, 0.4f, 6f);
    }

    private void FixedUpdate()
    {
        if (rb == null || path.Count == 0)
            return;
        PathSteps++;
        if (cursor >= path.Count)
        {
            rb.linearVelocity = Vector2.zero;
            return;
        }

        Vector2 target = path[cursor];
        Vector2 delta = target - rb.position;
        float step = Mathf.Max(0.05f, speed) * Time.fixedDeltaTime;
        if (delta.magnitude <= step)
        {
            // Land ON the waypoint: the dwell is what lets the thin trigger
            // circle report the overlap.
            rb.position = target;
            rb.linearVelocity = Vector2.zero;
            cursor++;
            return;
        }
        rb.linearVelocity = delta.normalized * Mathf.Max(0.05f, speed);
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        SolidContactFrames++;
        Collider2D c = collision.collider;
        if (c == null)
            return;
        if (!solidSeen.Contains(c))
        {
            solidSeen.Add(c);
            SolidColliderHits++;
            if (FirstSolidName.Length == 0)
            {
                // Name it and date it: a solid contact is only evidence if it
                // says which collider and on which step of the path.
                FirstSolidName = c.gameObject.name;
                FirstSolidStep = PathSteps;
            }
        }
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        SolidContactFrames++;
        if (FirstSolidStayStep == 0)
            FirstSolidStayStep = PathSteps;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        TriggerOverlapFrames++;
        if (other != null && !triggerSeen.Contains(other))
        {
            triggerSeen.Add(other);
            TriggerHits++;
        }
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        TriggerOverlapFrames++;
    }
}
