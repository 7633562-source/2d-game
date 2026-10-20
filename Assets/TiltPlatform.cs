using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

// TILT-PLATFORM STAND (owner 28.09, refs/tilt-platform-20260928.jpg): a 3 m deck on a
// central pivot axis. The owner's task: "платформа поворачивается относительно своей оси
// сначала на 1°, потом на 2°, и так далее; человек должен устоять".
//
// The ramp: the deck moves to +1°, holds, moves to -1°, holds, then +2° / -2°, ... up
// to maxStepDeg. Each move is eased (smoothstep) over stepMoveSeconds and each hold
// lasts stepHoldSeconds - the owner's first pass moved linearly and read as jerky
// ("сейчас она рывками двигается"), so the move is now slow, ramped at both ends.
// The drive is kinematic and deterministic (no randomness): a take is reproducible.
// The foot contact is plain friction: the deck is driven, the man must hold himself -
// that is what the stand measures.
//
// The editor Play path builds it through GameProcess (the tiltPlatform field). The trial
// wiring (-tiltPlatform args in HeadlessTrial) is the lanes' follow-up.
public class TiltPlatform : MonoBehaviour
{
    [Header("Deck (the owner's drawing: on a central axis; 5 m per owner 29.09)")]
    [Tooltip("Deck length, m.")]
    public float deckLength = 5.0f;
    [Tooltip("Deck thickness, m.")]
    public float deckThickness = 0.06f;
    [Tooltip("Friction between the deck and the feet (Unity averages it with the sole's).")]
    public float deckFriction = 0.9f;

    [Header("Ramp")]
    [Tooltip("Seconds before the ramp starts (the onset rule: the first stand is not counted).")]
    public float settleSeconds = 1.5f;
    [Tooltip("Seconds of one move between two angles (eased at both ends).")]
    public float stepMoveSeconds = 0.9f;   // owner 29.09: faster - the rocker must not crawl
    [Tooltip("Seconds the deck holds an angle before the next move.")]
    public float stepHoldSeconds = 1.5f;   // owner 29.09: faster (was 3.0)
    [Tooltip("First step, degrees; the ramp adds stepIncrementDeg per step.")]
    public float firstStepDeg = 10f;   // owner 29.09: start big, no crawling 1-2-3
    [Tooltip("Largest step, degrees; the ramp stays there and keeps rocking.")]
    public float maxStepDeg = 48f;     // owner 29.09: the full tested range
    public float stepIncrementDeg = 5f; // owner 29.09: bigger angles per step

    // ROUND 16 (the owner's addendum 8, the row of decks): deck k's ramp clock is
    // shifted by k * rowPhaseSeconds, so the row can rock in phase (0, the default)
    // or with a settable rhythm. A 0 offset returns the deck's own clock untouched.
    [Tooltip("Row phase: deck k's ramp clock is shifted by k * this (s); 0 = in phase.")]
    public float phaseOffsetSeconds = 0f;

    Rigidbody2D body;
    float rampTime;
    float angleDeg;
    int lastStep = -1;
    // ROUND 15: the drive's own jump invariant (see FixedUpdate).
    float lastTargetDeg;
    bool hasLastTarget;

    public int CurrentStep { get; private set; }
    public float CurrentAngleDeg { get { return angleDeg; } }
    // The drive's own clock, seconds since the deck was built (29.09, the lane's
    // per-step log: TiltStandLog derives the step schedule from the same clock the
    // drive runs on, so a row can never name a different angle than the deck gave).
    public float RampTime { get { return rampTime; } }

    // ROUND 15 (the plant-phase ejection instrument, -tiltPen 1): the deck's top
    // face at a world X. A planted palm's penetration is DeckTopWorldY(hand.x) minus
    // the palm's own bottom face; the log reads this every step. Read-only, the same
    // corner maths the collider carries (local (u, +t/2) rotated by the deck).
    public float DeckTopWorldY(float worldX)
    {
        float rad = angleDeg * Mathf.Deg2Rad;
        return transform.position.y
            + Mathf.Cos(rad) * 0.5f * deckThickness
            + (worldX - transform.position.x) * Mathf.Sin(rad);
    }

    // ROUND 13 (the get-up practice): -tiltGetUpHold 1 has the trial freeze the ramp
    // the moment the fall fires, so the rise is practiced on a steady deck. The
    // freeze only ever engages after a fall in get-up mode; every other take runs
    // the ramp exactly as before (false on every frame of every pre-existing row).
    public bool rampFrozen;

    // ROUND 35: THE LEVEL-EASE HOLD - the r34 practice parked the frozen deck at the
    // fall's own slope (an uphill get-up). With rampFrozenEaseDegPerSec > 0 (the trial's
    // -tiltGetUpHoldEase) the frozen deck walks to level (0 deg) at that rate. 0 = the
    // r13 freeze byte for byte (the schedule keeps repeating at the frozen clock).
    public float rampFrozenEaseDegPerSec;
    float frozenEaseAngle;
    bool frozenEaseSeeded;

    // ROUND 40: THE FREEZE WINDOW (the r39 push's own discriminator). With
    // freezeFromSeconds >= 0 the deck's clock - and so the whole schedule: the
    // angle, the steps, RampTime - PAUSES for freezeLenSeconds (0 = to the end)
    // and resumes seamlessly where it stopped, so an attempt's push runs on a
    // still surface. Default -1: no window, every pre-existing take byte for byte.
    public float freezeFromSeconds = -1f;
    public float freezeLenSeconds;
    float freezeClock;      // the window's own elapsed, s
    bool freezeActive;
    bool freezeDone;

    // ROUND 42: THE MOVE-GUARD (the practice rise vs a sweeping surface). The deck
    // reports whether its commanded angle is changing this frame; the pose drive's
    // move-guard (-poseGetUpMoveGuard) gates the rise clock on it. A row of decks
    // gates on the union of its members. Read-only instrumentation: nothing reads
    // it unless the flag is on, every pre-existing take keeps its bytes.
    public bool SurfaceMoving { get; private set; }
    static readonly List<TiltPlatform> live = new List<TiltPlatform>();

    public static bool AnyMoving()
    {
        for (int i = 0; i < live.Count; i++)
            if (live[i] != null && live[i].SurfaceMoving)
                return true;
        return false;
    }

    void OnDestroy()
    {
        live.Remove(this);
    }

    public static TiltPlatform Spawn(float centerX, float groundY, float lengthMeters)
    {
        return Spawn(centerX, groundY, lengthMeters, 0f);
    }

    // ROUND 15 (the plant-phase ejection price): the plate thickness is a knob. A
    // hand box is 0.16 m tall against a 0.06 m plate - it can thread the plate, and
    // the solver then throws the body (sitB, t 25.505). A thicker plate keeps the
    // contact on the top face. thickness <= 0.01 keeps the drawn plate (0.06); the
    // deck TOP stays exactly where it was in both cases (Build cancels the half).
    public static TiltPlatform Spawn(float centerX, float groundY, float lengthMeters,
        float thicknessMeters)
    {
        var go = new GameObject("TiltPlatform");
        go.transform.position = new Vector3(centerX, groundY, 0f);
        var platform = go.AddComponent<TiltPlatform>();
        if (lengthMeters > 0.1f)
            platform.deckLength = lengthMeters;
        if (thicknessMeters > 0.01f)
            platform.deckThickness = thicknessMeters;
        platform.Build();
        return platform;
    }

    void Build()
    {
        // The deck top sits a hair above the owner's ground line so the feet own the
        // contact; the world floor stays right underneath (where the scene has one).
        transform.position = new Vector3(
            transform.position.x,
            transform.position.y + 0.002f - 0.5f * deckThickness,
            0f);

        body = gameObject.AddComponent<Rigidbody2D>();
        body.bodyType = RigidbodyType2D.Kinematic;
        body.useFullKinematicContacts = true;
        body.interpolation = RigidbodyInterpolation2D.Interpolate;

        // The deck IS a support and must be sensed as one: the estimators and the
        // balance core look at the Ground layer only (BodyStateEstimator's foot
        // probes, GroundLayers.Mask), so a deck left on the default layer read as
        // empty air under the soles - the man was "ungrounded" while standing on
        // it. Same call GroundBuilder and StaticPlatform make (29.09).
        GroundLayers.Apply(gameObject);
        live.Add(this);   // ROUND 42: the move-guard's registry (see SurfaceMoving)

        var collider = gameObject.AddComponent<BoxCollider2D>();
        collider.size = new Vector2(deckLength, deckThickness);
        var material = new PhysicsMaterial2D("TiltDeck");
        material.friction = deckFriction;
        material.bounciness = 0f;
        collider.sharedMaterial = material;

        // A thin strip so the stand is readable on screen. The built-in white texture is
        // SQUARE: drawing it whole made a 3 x 3 m brown blob (owner: "исправь платформу").
        // The strip is a child sprite of one square metre scaled to the deck's own
        // geometry; the collider on the parent keeps its size.
        var deckVisual = new GameObject("DeckVisual");
        deckVisual.transform.SetParent(transform, false);
        deckVisual.transform.localScale = new Vector3(deckLength, deckThickness, 1f);
        var deckRenderer = deckVisual.AddComponent<SpriteRenderer>();
        deckRenderer.sprite = SolidSprite();
        deckRenderer.color = new Color(0.72f, 0.52f, 0.30f, 1f);
        deckRenderer.sortingOrder = 1;

        // The pivot axis marker: a short stub under the centre (the owner's axis).
        var stub = new GameObject("Pivot");
        stub.transform.SetParent(transform, false);
        stub.transform.localPosition = new Vector3(0f, -0.30f, 0f);
        stub.transform.localScale = new Vector3(0.06f, 0.55f, 1f);
        var stubRenderer = stub.AddComponent<SpriteRenderer>();
        stubRenderer.sprite = SolidSprite();
        stubRenderer.color = new Color(0.35f, 0.30f, 0.26f, 1f);
        stubRenderer.sortingOrder = 0;
    }

    // One square world metre of white (the built-in white texture is 4 x 4 px).
    static Sprite SolidSprite()
    {
        return Sprite.Create(
            Texture2D.whiteTexture,
            new Rect(0f, 0f, Texture2D.whiteTexture.width, Texture2D.whiteTexture.height),
            new Vector2(0.5f, 0.5f),
            Texture2D.whiteTexture.width);
    }

    // Where the man's soles are: the deck top goes here. This is the project's own spawn
    // convention - the world's ground top is -2.0 and the spawn line is -0.82 ("feet
    // touch the ground"), so the root sits 1.18 m above the soles. The collider scan
    // was tried first and proved unreliable at spawn time (the deck landed at the ROOT
    // instead - the owner's screenshot: the man caught on the beam at his hips with the
    // legs dangling). Deterministic beats clever here.
    public static float LowestContactY(GameObject humanObject)
    {
        return humanObject.transform.position.y - 1.18f;
    }

    void FixedUpdate()
    {
        if (body == null)
            return;

        if (!rampFrozen)
        {
            // ROUND 40: the freeze window (default -1 = the plain clock).
            if (freezeFromSeconds >= 0f)
            {
                if (!freezeDone && !freezeActive && rampTime >= freezeFromSeconds)
                {
                    freezeActive = true;
                    freezeClock = 0f;
                    Debug.Log("TiltPlatform: freeze window t=" + rampTime.ToString("0.###", CultureInfo.InvariantCulture)
                        + " deg=" + angleDeg.ToString("0.##", CultureInfo.InvariantCulture)
                        + " len=" + freezeLenSeconds.ToString("0.##", CultureInfo.InvariantCulture));
                }
                if (freezeActive)
                {
                    freezeClock += Time.fixedDeltaTime;
                    if (freezeLenSeconds > 0f && freezeClock >= freezeLenSeconds)
                    {
                        freezeActive = false;
                        freezeDone = true;
                        Debug.Log("TiltPlatform: freeze window end t=" + rampTime.ToString("0.###", CultureInfo.InvariantCulture));
                    }
                }
                else
                {
                    rampTime += Time.fixedDeltaTime;
                }
            }
            else
            {
                rampTime += Time.fixedDeltaTime;
            }
        }

        // ROUND 16 (the row): the schedule runs on the deck's own clock shifted by its
        // phase offset. The branch keeps the single deck's clock BIT-EXACT (a 0 offset
        // returns the same float), so every pre-existing row holds byte for byte.
        float rampClock = phaseOffsetSeconds == 0f ? rampTime : rampTime + phaseOffsetSeconds;

        float targetDeg = 0f;
        int step = 0;
        if (rampClock > settleSeconds)
        {
            float t = rampClock - settleSeconds;
            float move = Mathf.Max(0.2f, stepMoveSeconds);
            float hold = Mathf.Max(0.2f, stepHoldSeconds);
            float leg = move + hold;      // one side of the rock: the move, then the hold
            int side = Mathf.FloorToInt(t / leg);
            float within = t - side * leg;
            step = 1 + side / 2;
            step = Mathf.Min(step, Mathf.Max(1, Mathf.RoundToInt((maxStepDeg - firstStepDeg) / Mathf.Max(0.5f, stepIncrementDeg)) + 1));
            bool plus = (side % 2) == 0;
            // ROUND 15 (the plant-phase ejection): the from-angle must use the
            // UNCLAMPED ramp index. The clamp below caps `step` at the top of the
            // ramp, so a clamped plus-side read fromDeg = -(first + (2-2)*incr) =
            // -firstStep while the deck really stood at -maxStep: the target then
            // JUMPED 5 deg in one frame at the turnaround (measured sitB: the deck
            // went -15.000 -> -9.968 in one step, whipping a planted palm at ~35 m/s
            // and ejecting the body). stepRaw keeps the from-angle continuous.
            int stepRaw = 1 + side / 2;
            float stepDeg = Mathf.Min(firstStepDeg + (step - 1) * stepIncrementDeg, maxStepDeg);
            float toDeg = plus ? stepDeg : -stepDeg;
            float fromDeg;
            if (side == 0)
                fromDeg = 0f;                                              // level at the start
            else if (plus)
                fromDeg = -Mathf.Min(firstStepDeg + (stepRaw - 2) * stepIncrementDeg, maxStepDeg); // the previous step's minus
            else
                fromDeg = stepDeg;                                         // this step's own plus

            if (within < move)
            {
                float u = within / move;
                u = u * u * (3f - 2f * u);                                 // smoothstep: no jerk at either end
                targetDeg = Mathf.Lerp(fromDeg, toDeg, u);
            }
            else
            {
                targetDeg = toDeg;
            }
        }

        // ROUND 35: the level-ease hold (see rampFrozenEaseDegPerSec). Engaged only
        // while frozen AND the knob is on, so every other row holds its bytes.
        if (rampFrozen && rampFrozenEaseDegPerSec > 0f)
        {
            if (!frozenEaseSeeded)
            {
                frozenEaseAngle = angleDeg;
                frozenEaseSeeded = true;
            }
            frozenEaseAngle = Mathf.MoveTowards(frozenEaseAngle, 0f,
                rampFrozenEaseDegPerSec * Time.fixedDeltaTime);
            targetDeg = frozenEaseAngle;
        }
        else
        {
            frozenEaseSeeded = false;
        }

        if (step != lastStep)
        {
            lastStep = step;
            if (step > 0)
                Debug.Log(string.Format(
                    "TiltPlatform: step {0} at +-{1:0.#} deg (t = {2:0.0} s)",
                    step, Mathf.Min(firstStepDeg + (step - 1) * stepIncrementDeg, maxStepDeg), rampTime));
        }

        // ROUND 15: the drive's own invariant - a kinematic deck may never jump more
        // than the smoothstep's own slew (~0.25 deg/step at 30 deg over 0.9 s);
        // anything past 1 deg in one step is a discontinuity bug and is named with
        // its clock. Log only: the drive itself never reads this.
        if (hasLastTarget)
        {
            float jump = Mathf.Abs(targetDeg - lastTargetDeg);
            if (jump > 1f)
                Debug.LogWarning("TILTPLATFORM JUMP "
                    + jump.ToString("0.###", CultureInfo.InvariantCulture)
                    + " deg in one step at t=" + rampTime.ToString("0.###", CultureInfo.InvariantCulture));
            // ROUND 42: the move-guard's own signal - the commanded angle's
            // per-frame delta. Holds, the freeze window and a frozen deck all
            // read still.
            SurfaceMoving = jump > 1e-3f;
        }
        lastTargetDeg = targetDeg;
        hasLastTarget = true;

        angleDeg = targetDeg;
        body.MoveRotation(angleDeg);
        CurrentStep = step;
    }
}
