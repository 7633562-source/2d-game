using UnityEngine;

// Parasitic drag of the body and tail. Without it the flap accelerates the
// bird with no ceiling: the wing makes force, but the fuselage has no oncoming flow.
// No lift here — only quadratic drag against center velocity.
[DefaultExecutionOrder(50)]
public class BodyAero : MonoBehaviour
{
    [Header("Frontal area, m²")]
    public float frontalArea = 0.008f;

    [Header("Drag coefficient")]
    public float dragCoefficient = 0.35f;

    public float airDensity = 1.2f;
    public float minSpeed = 0.3f;
    public float maxForce = 25f;

    public bool generateForce = true;
    public Vector2 lastForce;

    private Rigidbody2D rb;
    private BirdFlight flight;
    private BirdController control;
    private bool controlLookupDone;
    private bool huntDragLatched;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        flight = GetComponent<BirdFlight>();
        control = GetComponentInParent<BirdController>();
        controlLookupDone = control != null;
        if (rb == null)
            Debug.LogError("BodyAero needs a Rigidbody2D on the same object.");
    }

    // BirdController.Awake: this Awake ran while the root controller
    // was still missing (BuildFlock order). One write, no Find.
    public void BindControl(BirdController hunt)
    {
        if (hunt != null)
            control = hunt;
        controlLookupDone = true;
    }

    void FixedUpdate()
    {
        // BirdFlight.Tick always calls Apply (flock and ragdoll). A
        // second pass here doubled drag and ate hunt walkThrust on
        // the close. drivenExternally covers the flock; ragdoll still
        // ticks BirdFlight in its own FixedUpdate. Cached, no Find.
        if (flight != null && flight.enabled)
            return;
        if (!generateForce) return;
        Apply();
    }

    // The same quadratic drag. BirdFlight calls this from its own step
    // so the flock does not get a second FixedUpdate on the same force.
    public void Apply()
    {
        lastForce = Vector2.zero;
        if (!generateForce || rb == null) return;
        // BuildFlock adds BodyAero on Body before BirdController on the
        // root, so Awake saw null. Resolve once. No FindObjectOfType.
        if (control == null && !controlLookupDone)
        {
            control = GetComponentInParent<BirdController>();
            controlLookupDone = true;
        }
        // Hunt Walk after Forage bind: leftover Fly drag stays off via
        // IsHuntSoil (preferDirtLand, dirt Walk near the plate, dirt Peck,
        // or dirt-height Peck with a leftover pad after flicker). Cache
        // holds; no per-step Find. Quadratic drag ate walkThrust on the
        // close (vx → 0 before OnDirtPlate), so HuntBeak walked off the
        // Insect before BeakStrike Rel Hostile. Hurt is the beak (body-X
        // HuntBeak strip, PeckHold both ways, Dynamic solid vs the
        // Insect kinematic trigger, live only on Peck/Attack so hunt
        // Walk is not a 200 Hz island). This drag gate stays on
        // IsHuntSoil so Walk still closes. Keep drag off one step if
        // preferDirtLand drops before Hurt — body HoldSimulatedSupport
        // / OnHuntDirt / walkThrust share that IsHuntSoil gate too.
        // Resting flock (energyRest) does not need aero — NpcRest may
        // leave this behaviour enabled (PERF-5). Cached control, no
        // FindObjectOfType.
        if (control != null && control.energyRest)
        {
            lastForce = Vector2.zero;
            return;
        }
        if (control != null && control.IsHuntSoil())
        {
            huntDragLatched = true;
            lastForce = Vector2.zero;
            return;
        }
        if (huntDragLatched)
        {
            huntDragLatched = false;
            lastForce = Vector2.zero;
            return;
        }

        Vector2 v = rb.linearVelocity;
        float speed = v.magnitude;
        if (speed < minSpeed) return;

        // F = −½ ρ v² S Cd · v̂
        float mag = 0.5f * airDensity * speed * speed * frontalArea * dragCoefficient;
        mag = Mathf.Min(mag, maxForce);
        Vector2 force = -v / speed * mag;
        rb.AddForce(force, ForceMode2D.Force);
        lastForce = force;
    }
}
