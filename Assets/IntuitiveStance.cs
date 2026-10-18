using System.Globalization;
using UnityEngine;

// ─── THE INTUITIVE STANCE (29.09, task "Tilt-Platform Stand"; owner's addendum 2) ───
//
// Owner (verbatim, RU): "человек сам должен интуитивно менять стойку в зависимости от
// окружающих факторов. Иногда ему удобно стоять, ноги близкие иногда расставлять. Надо
// тоже прорабатывать над этим механизмом." - the stance is HIS decision, not a run param:
// NARROW when calm, WIDE as the disturbance rises, and the transitions happen by a REAL
// STEP (the foot leaves the deck, travels, and plants) - never by teleporting the feet.
//
// WHAT. A layer on the standing man on the tilt stand (TiltPlatform.cs). It reads the
// deck's own angle (the disturbance) and drives the stance:
//   WIDE   - past widenDeg of deck tilt the man widens his base: the front foot goes
//            forward, the back foot back, until the feet's separation reaches the goal
//            (sep0 + isSpreadM).
//   NARROW - once the deck has been quiet (|angle| <= narrowDeg for calmHoldS) he brings
//            the separation back toward the base the take gave him.
//
// HOW (and why this shape). Three smoke takes of round 5 taught the rig's real physics:
//   - a CLOSED-LOOP servo that chases an absolute foot position fights the balance core's
//     own live foot motions (drags of 3-20 cm during the holds): the placements overshot,
//     the feet crossed, and 19 churn steps of an earlier build walked the man off the
//     deck while a control on the same ramp stood;
//   - the ONE pattern that ever moved a foot well is the fall reflex's own (FallReflex.cs):
//     drive the hip to a fixed angle for a short window on a foot that is ALREADY free,
//     then hand the leg back to the rig (round 4's record: a floating foot travelled
//     14 cm in 0.2 s under exactly that drive);
//   - the rig unloads/raises feet on its own all the time (the float events; the probe's
//     grounded bit even lags: the smoke's fzR fell 674 -> 112 with the sole 3.7 cm up
//     while the flag still said grounded - hence Airborne() below).
// So the component never lifts a loaded foot and never chases a position. It waits for
// the rig's OWN float, and once per float event gives that free foot one short, fixed
// push (~isHipSwing degrees) toward where the man wants his stance. Over the float
// events of a ramp the stance converges; between them the component writes nothing.
//
// SAFETY (a stance step must never cost a fall): a push starts only while the body is
// Standing, the fall reflex is not latched, the other foot is grounded, the support
// margin is above marginGateM, the deck is not slewing faster than rateMaxDegPerSec,
// and the cooldown has run out. A push ends at once if the margin dives below
// abortMarginM, the body stops Standing or the reflex fires. One push per float event,
// at most maxPushPerSide per side per take.
//
// KNOBS (read once in Awake; every one has a default that keeps the landed behaviour):
//   -isSpreadM m     the widen goal: separation target = base + this (default 0.05)
//   -isWidenDeg d    deck tilt at which the man starts widening (default 8)
//   -isNarrowDeg d   deck tilt below which, calm, he steps back in (default 4)
//   -isCalmHold s    seconds of calm before he narrows (default 2.0)
//   -isMargin m      support-margin gate to start a push (default 0.05)
//   -isAbortMargin m margin below which the active push aborts (default 0.02)
//   -isCooldown s    seconds between pushes (default 1.0)
//   -isPushS s       one push's drive window (default 0.30)
//   -isHipSwing d    hip drive of one push, minus = forward (default 8)
//   -isKneeRaise d   knee fold that keeps the toe clear while the foot travels (default 12)
//   -isAirClear m    sole clearance that reads as "foot up" (default 0.02)
//   -isRate d/s      deck slew gate for starting pushes (default 6)
//   -isActive 0/1    the man also takes the deliberate step as soon as the free foot
//                    shows (default 1): a step on a foot that carries no weight -
//                    never on the support foot
//   -isActiveMargin m margin gate for a deliberate (free-foot) step (default 0.10)
//   -isMaxPush n     per-side push cap for a take (default 6)
//   -isLog 0/1       event + trace lines (default 1)
//   -intuitiveStance 0/1 arms the whole component (the trial passes it; the played
//                    scene arms it by default). Disarmed = it writes nothing at all.
[DefaultExecutionOrder(22)]
public class IntuitiveStance : MonoBehaviour
{
    private struct Drive
    {
        public HingeJoint2D joint;
        public Muscle flexor;
        public Muscle extensor;
        public float inertia;
    }

    // ─── the situation ───
    public TiltPlatform platform;    // assigned by the spawner that built the deck

    // ─── knobs ───
    private bool armed = true;
    private float spreadM = 0.05f;
    private float widenDeg = 8f;
    private float narrowDeg = 4f;
    private float calmHoldS = 2f;
    private float marginGateM = 0.05f;
    private float abortMarginM = 0.02f;
    private float cooldownS = 1f;
    private float pushS = 0.30f;
    private float hipSwingDeg = 8f;
    private float kneeRaiseDeg = 12f;
    private float airClearM = 0.02f;
    private float rateMaxDegPerSec = 6f;
    private bool activeOn = true;
    private float activeMarginM = 0.10f;
    private int maxPushPerSide = 4;
    private bool logTrace = true;

    private const int floatGateFrames = 10;    // frames of "up" before a float may be pushed
    private const float StepSlew = 60f;        // the fall reflex's own slew: outruns the balance core

    private BalanceController balance;
    private BodyStateEstimator state;
    private FallReflex reflex;

    private Drive hipL, hipR, kneeL, kneeR;

    private float offL0, offR0;        // the feet's own X at start (the base the take gave)
    private float sep0;                // and the separation they started with
    private float lastAngle;
    private float calmTime;
    private float cooldown;
    private bool widenLatch;           // the man's own decision: wide stance engaged
    private bool everWidened;          // a widen has landed: only then is there anything to narrow
    private int floatFramesL, floatFramesR;
    private bool floatDoneL, floatDoneR; // one push per float event, no re-fires
    private int pushesL, pushesR;
    private int stanceSteps;
    private float logClock;

    // the push (a real step: the FREE foot is driven to its new place, then the rig
    // owns the landing again). Phase 0 = the unload fold of a deliberate step; phase 1
    // = the drive window. A float-started push begins straight in phase 1.
    private bool pushing;
    private int pushSide;
    private int pushPhase;
    private float pushUntil;
    private float pushStart;
    private float pushHipDeg;
    private bool pushActive;
    private string pushCause;

    // the sole-load meter (the same read the recorder's fzL/fzR columns use): the
    // solver's contact impulses on the four sole plates. This is what names the FREE
    // foot - the smoke take showed the man standing at +-8 deg with fzR 674 N and
    // fzL 6.6 N, so "which foot is free" cannot be read from the grounded bit.
    private Collider2D[] soles = new Collider2D[4];
    private readonly ContactPoint2D[] contacts = new ContactPoint2D[8];
    private ContactFilter2D groundFilter;
    private bool groundFilterReady;
    private float fzL, fzR;

    public bool Armed { get { return armed; } }
    public int StanceSteps { get { return stanceSteps; } }
    public bool Wide { get { return widenLatch; } }

    void Awake()
    {
        armed = GetFloat("-intuitiveStance", armed ? 1f : 0f) > 0.5f;
        spreadM = Mathf.Clamp(GetFloat("-isSpreadM", spreadM), 0.005f, 0.20f);
        widenDeg = Mathf.Clamp(GetFloat("-isWidenDeg", widenDeg), 0.5f, 45f);
        narrowDeg = Mathf.Clamp(GetFloat("-isNarrowDeg", narrowDeg), 0f, 40f);
        calmHoldS = Mathf.Clamp(GetFloat("-isCalmHold", calmHoldS), 0.1f, 60f);
        marginGateM = GetFloat("-isMargin", marginGateM);
        abortMarginM = GetFloat("-isAbortMargin", abortMarginM);
        cooldownS = Mathf.Clamp(GetFloat("-isCooldown", cooldownS), 0.1f, 30f);
        pushS = Mathf.Clamp(GetFloat("-isPushS", pushS), 0.05f, 2f);
        hipSwingDeg = Mathf.Clamp(GetFloat("-isHipSwing", hipSwingDeg), 1f, 40f);
        kneeRaiseDeg = Mathf.Clamp(GetFloat("-isKneeRaise", kneeRaiseDeg), 0f, 60f);
        airClearM = Mathf.Clamp(GetFloat("-isAirClear", airClearM), 0.005f, 0.10f);
        rateMaxDegPerSec = Mathf.Clamp(GetFloat("-isRate", rateMaxDegPerSec), 0.1f, 60f);
        activeOn = GetFloat("-isActive", activeOn ? 1f : 0f) > 0.5f;
        activeMarginM = GetFloat("-isActiveMargin", activeMarginM);
        maxPushPerSide = Mathf.Clamp(Mathf.RoundToInt(GetFloat("-isMaxPush", maxPushPerSide)), 1, 60);
        logTrace = GetFloat("-isLog", logTrace ? 1f : 0f) > 0.5f;
    }

    void Start()
    {
        balance = GetComponent<BalanceController>();
        state = GetComponent<BodyStateEstimator>();
        reflex = GetComponent<FallReflex>();
        if (balance == null || state == null)
        {
            Debug.LogWarning("INTUITIVESTANCE: no BalanceController/BodyStateEstimator on " + name + " - inert");
            enabled = false;
            return;
        }
        if (platform == null)
        {
            Debug.LogWarning("INTUITIVESTANCE: no deck assigned on " + name + " - inert");
            enabled = false;
            return;
        }
        if (!armed)
        {
            enabled = false;
            return;
        }
        hipL = FindDrive("LeftLegThigh");
        hipR = FindDrive("RightLegThigh");
        kneeL = FindDrive("LeftLegShin");
        kneeR = FindDrive("RightLegShin");
        offL0 = state.leftFootPosition.x;
        offR0 = state.rightFootPosition.x;
        sep0 = Sep();
        soles[0] = SegmentCollider("LeftLegFoot");
        soles[1] = SegmentCollider("LeftLegToe");
        soles[2] = SegmentCollider("RightLegFoot");
        soles[3] = SegmentCollider("RightLegToe");
        groundFilter = new ContactFilter2D();
        groundFilter.SetLayerMask(GroundLayers.Mask);
        groundFilter.useTriggers = false;
        groundFilterReady = true;
        Debug.Log("INTUITIVESTANCE armed spread=" + F(spreadM)
            + " widen=" + F(widenDeg) + " narrow=" + F(narrowDeg)
            + " gate=" + F(marginGateM) + " push=" + F(pushS) + "s swing=" + F(hipSwingDeg)
            + " baseSep=" + F(sep0));
    }

    void FixedUpdate()
    {
        float dt = Time.fixedDeltaTime;

        float a = Mathf.Abs(platform.CurrentAngleDeg);
        float rate = Mathf.Abs(a - lastAngle) / Mathf.Max(1e-6f, dt);
        lastAngle = a;

        bool standing = balance.locomotionState == BalanceController.LocomotionState.Standing;
        bool reflexLive = reflex != null && reflex.Fired;
        float margin = state.supportMargin;

        // ── the man's own decision ──
        if (a >= widenDeg)
            widenLatch = true;
        if (a <= narrowDeg)
            calmTime += dt;
        else
            calmTime = 0f;
        if (calmTime >= calmHoldS)
            widenLatch = false;

        cooldown = Mathf.Max(0f, cooldown - dt);

        // float bookkeeping (both sides, always): a side is a step candidate when it is
        // UP (the probe/clearance read) or FREE (the load read: under a quarter of the
        // body's weight on that sole). The smoke take showed the man standing at +-8 deg
        // on ONE foot (fzR 674 vs fzL 6.6) - the free foot is where a step may happen;
        // the loaded one is the support and must never be folded.
        fzL = SoleLoad(0);
        fzR = SoleLoad(1);
        if (Airborne(0) || Free(0)) floatFramesL++; else { floatFramesL = 0; floatDoneL = false; }
        if (Airborne(1) || Free(1)) floatFramesR++; else { floatFramesR = 0; floatDoneR = false; }

        float offL = state.leftFootPosition.x - offL0;
        float offR = state.rightFootPosition.x - offR0;

        // the standing trace: every 0.5 s, whatever the policy decides below.
        if (logTrace)
        {
            logClock -= dt;
            if (logClock <= 0f)
            {
                logClock = 0.5f;
                Debug.Log(string.Format(CultureInfo.InvariantCulture,
                    "INTUITIVESTANCE t={0:F2} a={1:F2} rate={2:F2} wide={3} ever={4} offL={5:F3} offR={6:F3} sep={7:F3} fz={8:F0}/{9:F0} margin={10:F3} steps={11} pu={12}",
                    Time.time, a, rate, widenLatch ? 1 : 0, everWidened ? 1 : 0, offL, offR, Sep(),
                    fzL, fzR,
                    state.supportMargin, stanceSteps,
                    pushing ? (pushSide == 0 ? "L" : "R") : "-"));
            }
        }

        // ── the active push runs to its window's end ──
        if (pushing)
        {
            if (!standing || reflexLive || margin < abortMarginM)
            {
                EndPush(false);
                return;
            }
            if (Time.time >= pushUntil)
            {
                EndPush(true);
                return;
            }
            // the drive window: the hip swings the free foot; the knee stays near
            // straight (the one clean 14 cm travel of the smoke takes ran with the knee
            // at zero; a folded knee drags the foot back, net foot ~ hip + knee).
            DriveJointToAngle(pushSide == 0 ? hipL : hipR, pushHipDeg,
                balance.hipPGain, balance.hipDGain, balance.errorReferenceDegrees);
            DriveJointToAngle(pushSide == 0 ? kneeL : kneeR, kneeRaiseDeg * 0.33f,
                balance.kneePGain, balance.kneeDGain, balance.errorReferenceDegrees);
            return;
        }

        // ── may a new push start? ──
        if (!standing || reflexLive)
            return;
        if (margin < marginGateM || cooldown > 0f || rate > rateMaxDegPerSec)
            return;

        // At rest before any widen there is nothing to correct: the base stance IS the
        // narrow stance. (Three smoke takes proved that "correcting" the ankle
        // strategy's own settle drift churns the man off the deck.)
        if (!widenLatch && !everWidened)
            return;

        float sep = Sep();
        float goalWide = sep0 + spreadM;
        float goalHome = sep0 + Mathf.Min(0.02f, spreadM * 0.4f);
        bool wantWide = widenLatch && sep < goalWide - 0.01f;
        bool wantNarrow = !widenLatch && everWidened && sep > goalHome;
        if (!wantWide && !wantNarrow)
            return;
        string cause = wantWide ? "widen" : "narrow";

        // A foot the rig itself has already unloaded is the man's chance: steer it while
        // it is up. Front foot forward, back foot back (widening); the reverse to come
        // home. One push per float event; the rest of the take writes nothing.
        // A foot the rig has left free is the man's chance: steer it while it is free.
        if (Ready(1) && OtherLoaded(1))
        {
            Fire(1, cause, wantWide);
            return;
        }
        if (Ready(0) && OtherLoaded(0))
        {
            Fire(0, cause, wantWide);
            return;
        }

        // No long-running float on offer: the man takes the chance as soon as the free
        // foot shows (3 frames) - a deliberate step on a foot that carries no weight,
        // gated by a calmer margin. Never a chase, never the support foot.
        if (activeOn && margin >= activeMarginM && pushesR < maxPushPerSide
            && floatFramesR >= 3 && OtherLoaded(1))
        {
            Fire(1, cause, wantWide);
            return;
        }
        if (activeOn && margin >= activeMarginM && pushesL < maxPushPerSide
            && floatFramesL >= 3 && OtherLoaded(0))
        {
            Fire(0, cause, wantWide);
            return;
        }
    }

    private bool Ready(int side)
    {
        if (side == 0)
            return !floatDoneL && floatFramesL >= floatGateFrames && pushesL < maxPushPerSide;
        return !floatDoneR && floatFramesR >= floatGateFrames && pushesR < maxPushPerSide;
    }

    private void Fire(int side, string cause, bool widen)
    {
        pushSide = side;
        pushCause = cause;
        pushing = true;
        pushStart = Time.time;
        pushActive = !Airborne(side);
        pushPhase = 1;
        pushUntil = Time.time + pushS;
        stanceSteps++;
        if (side == 0) { floatDoneL = true; pushesL++; } else { floatDoneR = true; pushesR++; }
        // minus = forward (the rig's own convention, FallReflex "stepping hip target,
        // minus = forward"): widen = front(R) forward, back(L) back; narrow = reverse.
        float forward = side == 1 ? -1f : 1f;
        pushHipDeg = forward * (widen ? hipSwingDeg : -hipSwingDeg);
        if (logTrace)
            Debug.Log(string.Format(CultureInfo.InvariantCulture,
                "INTUITIVESTANCE push t={0:F3} side={1} cause={2} lift={3} hip={4:F1} from={5:F3} sep={6:F3} margin={7:F3}",
                Time.time, side == 0 ? "L" : "R", cause, pushActive ? 1 : 0, pushHipDeg,
                side == 0 ? state.leftFootPosition.x : state.rightFootPosition.x,
                Sep(), state.supportMargin));
    }

    private void EndPush(bool completed)
    {
        pushing = false;
        cooldown = pushActive ? Mathf.Max(cooldownS, 2f) : cooldownS;
        float sep = Sep();
        // the man's own bookkeeping: wide once a widen push has run its window; home
        // again once a coming-home push brought the feet back near the base.
        if (completed && pushCause == "widen")
            everWidened = true;
        if (completed && pushCause == "narrow" && !widenLatch && sep <= sep0 + Mathf.Min(0.02f, spreadM * 0.4f))
            everWidened = false;
        if (logTrace)
            Debug.Log(string.Format(CultureInfo.InvariantCulture,
                "INTUITIVESTANCE push-end t={0:F3} side={1} cause={2} done={3} x={4:F3} sep={5:F3} wide={6} ever={7}",
                Time.time, pushSide == 0 ? "L" : "R", pushCause, completed ? 1 : 0,
                pushSide == 0 ? state.leftFootPosition.x : state.rightFootPosition.x,
                sep, widenLatch ? 1 : 0, everWidened ? 1 : 0));
    }

    // "Up" as the rig's own columns read it: the probe's grounded bit lags behind the
    // load (see the smoke take), so the sole's clearance has a vote too.
    private bool Airborne(int side)
    {
        if (side == 0)
            return !state.leftFootGrounded || state.leftFootGroundClearance > airClearM;
        return !state.rightFootGrounded || state.rightFootGroundClearance > airClearM;
    }

    // The FREE foot: carries under a quarter of the body's weight. The support foot is
    // the one with the load, and it must never be folded for a stance step.
    private bool Free(int side)
    {
        float total = fzL + fzR;
        if (total <= 1f) return false;
        float mine = side == 0 ? fzL : fzR;
        return mine * 4f < total;
    }

    private bool OtherLoaded(int side)
    {
        float total = fzL + fzR;
        if (total <= 1f) return false;
        float other = side == 0 ? fzR : fzL;
        return other >= 0.5f * total;
    }

    // The same read the recorder's fzL/fzR columns use: the solver's contact impulses
    // on the two sole plates of the side, upward part only.
    private float SoleLoad(int side)
    {
        if (!groundFilterReady) return 0f;
        float fz = 0f;
        float dt = Time.fixedDeltaTime;
        if (dt <= 0f) dt = 0.005f;
        for (int i = side * 2; i < side * 2 + 2; i++)
        {
            Collider2D col = soles[i];
            if (col == null) continue;
            int n = col.GetContacts(groundFilter, contacts);
            int count = Mathf.Min(n, contacts.Length);
            for (int k = 0; k < count; k++)
            {
                ContactPoint2D cp = contacts[k];
                float vertical = cp.normalImpulse * cp.normal.y;
                if (vertical > 0f) fz += vertical / dt;
            }
        }
        return fz;
    }

    private Collider2D SegmentCollider(string segmentName)
    {
        Transform t = transform.Find(segmentName);
        return t != null ? t.GetComponent<Collider2D>() : null;
    }

    private float Sep()
    {
        return state.rightFootPosition.x - state.leftFootPosition.x;
    }

    // ─── the rig's own laws, replicated for the push window (as FallReflex does) ───

    private void DriveJointToAngle(Drive d, float target, float pGain, float dGain, float errorRef)
    {
        if (d.flexor == null || d.extensor == null || d.joint == null) return;
        if (d.joint.useLimits)
            target = Mathf.Clamp(target, d.joint.limits.min, d.joint.limits.max);
        float dt = Time.fixedDeltaTime;
        float angleForError = d.joint.jointAngle + d.joint.jointSpeed * dt;
        float error = Mathf.DeltaAngle(angleForError, target) / Mathf.Max(1f, errorRef);
        float speed = d.joint.jointSpeed / Mathf.Max(1f, balance.speedReferenceDegPerSec);
        float raw = pGain * error - dGain * speed;
        float damping = 0f;
        if (d.inertia <= 0f) d.inertia = InertiaOf(d.joint);
        if (d.inertia > 0f)
        {
            float k = dGain * d.flexor.maxTorque * Mathf.Rad2Deg
                / Mathf.Max(1f, balance.speedReferenceDegPerSec);
            damping = k * dt / d.inertia;
        }
        float signal = Mathf.Clamp(raw / (1f + damping), -1f, 1f);
        ApplySignal(signal, d.flexor, d.extensor, dt, StepSlew);
    }

    private void ApplySignal(float signal, Muscle flexor, Muscle extensor, float dt, float slewPerSecond)
    {
        if (signal >= 0f)
        {
            WriteMuscle(extensor, signal, dt, slewPerSecond);
            WriteMuscle(flexor, 0f, dt, slewPerSecond);
        }
        else
        {
            WriteMuscle(flexor, -signal, dt, slewPerSecond);
            WriteMuscle(extensor, 0f, dt, slewPerSecond);
        }
    }

    private void WriteMuscle(Muscle muscle, float targetActivation, float dt, float slewPerSecond)
    {
        if (muscle == null) return;
        float speed = slewPerSecond > 0f ? slewPerSecond : balance.muscleActivationSpeed;
        muscle.activation = Mathf.MoveTowards(muscle.activation, targetActivation, speed * dt);
    }

    private static float InertiaOf(HingeJoint2D joint)
    {
        float inertia = 0f;
        Rigidbody2D self = joint.GetComponent<Rigidbody2D>();
        if (self != null) inertia += self.inertia;
        if (joint.connectedBody != null) inertia += joint.connectedBody.inertia;
        return inertia;
    }

    private Drive FindDrive(string segmentName)
    {
        Drive d = new Drive();
        Transform t = transform.Find(segmentName);
        if (t == null) return d;
        d.joint = t.GetComponent<HingeJoint2D>();
        Muscle[] muscles = t.GetComponents<Muscle>();
        for (int i = 0; i < muscles.Length; i++)
        {
            if (muscles[i] == null) continue;
            if (muscles[i].direction > 0f) d.flexor = muscles[i];
            else d.extensor = muscles[i];
        }
        return d;
    }

    private static string F(float v)
    {
        return v.ToString("0.###", CultureInfo.InvariantCulture);
    }

    private static float GetFloat(string name, float fallback)
    {
        string[] args = LauncherArgs.Args();
        int index = System.Array.IndexOf(args, name);
        if (index < 0 || index + 1 >= args.Length) return fallback;
        return float.TryParse(args[index + 1], NumberStyles.Float,
            CultureInfo.InvariantCulture, out float value) ? value : fallback;
    }
}
