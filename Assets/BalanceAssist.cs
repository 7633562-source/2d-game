using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

// Balance assist ("tone"). The human keeps its full physical body - muscles, joint limits,
// honest falls - but no longer has to hold its balance from joint torques alone.
//
// The law: a bounded horizontal force keeps the capture point (CoM + v / omega0) inside the
// soles. It is applied to every segment in proportion to its mass - a uniform acceleration, like
// a slightly tilted gravity - so it moves the CoM without twisting the body. Zero while the
// capture point is inside the (smoothed) soles; past the edge it is gain * M * omega0^2 * excess
// (gain 1 exactly stops the capture point), capped at forceMaxBw of body weight and slew-limited.
//
// Honesty: the help has a budget (N*s) that drains with use and refills in a few seconds, so a
// small push is absorbed and a big one is not; it is zero after 0.1 s with both feet off the
// ground and fades with the trunk past tiltFade0..tiltFade1. Falls stay real falls.
//
// Standing (not walking, both feet down): -assistCenter <k> adds a weak spring of k (1/s^2)
// pulling the CoM toward the middle of the soles, damped, capped at centerMaxBw of body weight - a
// person stands with the weight over mid-foot, not parked at the heel edge. 0 (default) = off.
// Crouched (same conditions): -assistHoldK <k> blends that spring toward k and its cap toward
// holdMaxBw by crouchLevel. A deep squat on staggered feet is pendulum-unstable faster than the
// ankle can brake (omega0^2 ~ 18 1/s^2 at the bottom), so the hold needs k above that.
// -assistCenterFree 1: the spring does not drain the budget (the budget is for edge rescues).
// -assistHoldTail <s>: after the rise the hold decays over s seconds instead of following
// crouchLevel down - the rise's hip extension throws the CoM back just as the squat ends.
// -assistHoldKeep <s>: the hold stays at its squat peak through the whole rise and s seconds after
// it, then fades in 0.5 s (a delay, where holdTail is only a rate limit). 0 = off.
// -assistWalkDir 1: the walk's band extension follows the commanded direction (MotionIntent.moveX),
// not the CoM velocity - with the velocity a backward runaway mid-walk widened the REAR edge.
// -assistWalkStart <bw>: for the first walkStartSec of a walk, a push in the commanded direction
// that fades with speed (0 at 0.5 m/s): a walk started from rest after the squat never got going.
// -assistCenterOne <k>: the standing spring also works on ONE grounded foot, toward that foot's
// middle (k replaces centerK there). After a rise on staggered feet the rear foot hangs clear and
// the CoM parks on the front foot's heel edge, where the fall reflex throws its catch step. Once
// the body has stood on one foot for centerOneArm seconds, that foot's middle stays the spring's
// home even when the other foot touches again, until the next walk, squat or flight. 0 = off.
// Stance close (PoseDriveController -clStanceClose 1): while the pose drive names a home x
// (ClStanceHomeX, the front sole's middle), the standing spring pulls there instead of to the middle
// of the soles, on one foot or two, with at least homeK and the homeMaxBw cap: the weight goes onto
// the front foot so the rear one can step up beside it. No home named = nothing changes.
// -assistSettleWalk 1: while the pose drive runs the stop's arrest step, the assist keeps its walking
// rules (front edge extended, no centring spring); 0 = the standing rules from the key release on.
// -assistBrake <bw>: a speed governor for the stop's brake ramp (see the walk brake in FixedUpdate).
// -assistSettleDamp <c>: 1/s velocity damper on the arrest step (settle latch), cap -assistSettleDampMax,
// tail -assistSettleDampTail; 0 = off, off = byte-identical.
// -assistWalkShift <bw>: a walk entered with the CoM more than walkShiftMin behind the middle of
// the front foot (staggered feet: the stand centres the CoM in the gap between them, on neither
// sole) is pushed in the commanded direction until the CoM reaches that middle, at most
// walkStartSec; it fades with speed like the walk-start push and does not drain the budget. 0 = off.
// -assistStandUp <bw>: the post-walk stand lift - an upward force on every segment (a uniform
// acceleration, like the horizontal assist) while the walk key is up and a foot is down, from the
// first walk on; ramps in over -assistStandUpIn s. Mean <= 5 % BW / ceiling 10 % (owner's standing
// rule). -assistStandUpIn <s>: the ramp. 0 (default) = off, off = byte-identical.
// -assistStandUpHold <s> (req-240): after s s of quiet stand (both feet down, |comVelX| < 0.05) the
// lift ramps out over 1 s; any new walk re-arms it. 0 (default) = the lift stays on, byte-identical.
//
// Walking: the front edge of the band is extended by walkLead in the direction of travel (the
// next step catches the capture point there); the rear edge is not, so a backward runaway is held.
//
// Switch: -assist <level> (absent or 0 = the component is not added: the pure-physics version).
// -assistAnkle <scale> scales the stand's ankle CoM loop gains while the assist is on (1 = as is);
// -assistAnkleCrouch <scale> does the same only while the body is crouched (crouchLevel > 0), so
// the squat gets a soft ankle loop and the walk keeps its own.
// Test harness: -pushAt "10:+20,15:-20" horizontal impulses (N*s) on the torso at the given
// seconds of this component's clock; works with or without the assist.
// Log: <-out>/<-label>_assist.csv every step, <-label>_assist_summary.txt at the end.
[DefaultExecutionOrder(40)]
public class BalanceAssist : MonoBehaviour
{
    public float level = 1f;          // -assist: scales the force ceiling
    public float forceMaxBw = 0.25f;  // -assistFmax: ceiling, fraction of body weight
    public float gain = 1.5f;         // -assistGain: x M * omega0^2
    public float edgeInset = 0.03f;   // -assistInset: m inside each sole edge where help starts
    public float bandTau = 0.06f;     // -assistBandTau: s, low-pass of the sole band edges
    public float slewTime = 0.15f;    // -assistSlew: s from 0 to the full ceiling
    public float budgetNs = 50f;      // -assistBudget: N*s of help before it gives out
    public float refillSeconds = 3f;  // -assistRefill: s to refill an empty budget
    public float lostDistance = 0.6f; // -assistLost: m of excess where the body is surely gone
    public float walkLead = 0.5f;     // -assistWalkLead: m added to the FRONT edge while walking
    public float ankleScale = 1f;     // -assistAnkle: ankle CoM loop gain scale while on
    public float ankleCrouchScale = 1f; // -assistAnkleCrouch: extra scale while crouched
    public float centerK = 0f;        // -assistCenter: 1/s^2, standing CoM spring to mid-soles
    public float centerMaxBw = 0.05f; // -assistCenterMax: cap of the spring, fraction of body weight
    public float holdK = 0f;          // -assistHoldK: 1/s^2, the spring at full crouch; 0 = off
    public float holdMaxBw = 0.25f;   // -assistHoldMax: its cap at full crouch, fraction of body weight
    public bool centerFree;           // -assistCenterFree 1: the spring does not drain the budget
    public float holdTail = 0f;       // -assistHoldTail: s the hold takes to fade after the rise; 0 = follows crouch
    public float holdKeep = 0f;       // -assistHoldKeep: s the squat-peak hold is kept after the rise; 0 = off
    public bool walkDirByIntent;      // -assistWalkDir 1: band extension by moveX, not by velocity
    public float walkStartBw = 0f;    // -assistWalkStart: walk-start push, fraction of body weight; 0 = off
    public float walkStartSec = 1f;   // -assistWalkStartSec: its window from walk onset, s
    public float walkShiftBw = 0f;    // -assistWalkShift: walk-entry weight shift, fraction of body weight; 0 = off
    public float walkShiftMin = 0.05f; // -assistWalkShiftMin: m behind the front foot's middle that arms it
    public float homeK = 20f;         // -assistHomeK: 1/s^2, the spring while the pose drive names a home
    public float homeMaxBw = 0.15f;   // -assistHomeMax: its cap, fraction of body weight
    public bool settleAsWalk;         // -assistSettleWalk 1: the stop's arrest step counts as walking
    public float brakeBw = 0f;        // -assistBrake: cap of the walk brake, fraction of body weight; 0 = off
    public float brakeSpeed = 0.7f;   // -assistBrakeSpeed: m/s allowed at moveX 1 (scaled by |moveX|)
    public float brakeFloor = 0.3f;   // -assistBrakeFloor: m/s, the lowest speed the brake asks for
    public float settleDamp = 0f;     // -assistSettleDamp: 1/s, velocity damper on the arrest step; 0 = off
    public float settleDampMax = 0.10f; // -assistSettleDampMax: cap of that damper, fraction of body weight
    public float settleDampTail = 0f; // -assistSettleDampTail: s the damper is kept after the latch drops
    public float standUpBw = 0f;      // -assistStandUp: post-walk stand lift, fraction of body weight; 0 = off
    public float standUpIn = 0.5f;    // -assistStandUpIn: s the lift takes to ramp in
    public float standUpHold = 0f;    // -assistStandUpHold: s of quiet stand before the lift fades; 0 = off
    public float centerOneK = 0f;     // -assistCenterOne: 1/s^2, the standing spring on one foot; 0 = off
    public float centerOneArm = 0.5f; // -assistCenterOneArm: s on one foot before its middle is kept as the home
    public float tiltFade0 = 45f;     // -assistTilt0: trunk tilt where help starts to fade, deg
    public float tiltFade1 = 75f;     // -assistTilt1: trunk tilt with no help, deg
    public float airborneSeconds = 0.1f;
    public float toneRiseRate = 4f;   // 1/s
    public float toneFallRate = 20f;  // 1/s

    // Published state (log; readable by the recorder later).
    public float tone;
    public float forceX;
    public float capturePointX;
    public float bandMin;
    public float bandMax;
    public float excess;
    public float budget;
    public float standUpF;            // the stand lift actually applied this step (N)
    private float standUpAge;         // s the lift has been on since the key-up
    private bool standUpArmed;        // a walk has happened this take
    private readonly List<float> standUpSamples = new List<float>(); // lift per step as a BW fraction
    private readonly List<float> standUpOnSamples = new List<float>(); // lift per step of the stand window
    private float standUpQuiet;       // s of quiet stand since the last walk
    private bool standUpQuietSeen;    // the stand has been quiet (gate for the hold clock)
    private float standUpHoldF = 1f;  // hold multiplier: 1 until -assistStandUpHold ramps the lift out

    private string pushSpec;
    private readonly List<Vector2> pushes = new List<Vector2>(); // x = time, y = impulse
    private int nextPush;

    private BodyStateEstimator state;
    private BalanceController balance;
    private MotionIntent intent;
    private Rigidbody2D[] bodies;
    private Rigidbody2D torso;
    private float totalMass;
    private bool bound;
    private float clock;
    private bool bandSeeded;
    private float bandLo, bandHi;
    private float airTimer;
    private bool ankleScaled;
    private float ankleP0, ankleD0;

    private StreamWriter log;
    private readonly StringBuilder row = new StringBuilder(180);
    private string summaryPath;
    private readonly List<float> loadSamples = new List<float>(16384);
    private float weakToneSeconds;
    private float budgetMin = float.MaxValue;
    private float pushNow;
    private float crouchNow;
    private float holdLevel;
    private float holdKeepLevel;
    private float holdKeepTimer;
    private float walkAge;
    private float startPart;
    private float shiftPart;
    private PoseDriveController pose;
    private bool homeOn;
    private float velLp;
    private float brakePart;
    private float dampPart;
    private float dampTailAge;
    private bool shiftOn;
    private float shiftHomeX;
    private float footLength = 0.26f;
    private float centerPart;
    private float oneFootTimer;
    private bool oneHomeOn;
    private float oneHomeX;
    private bool oneMode;

    public static void AttachIfRequested(GameObject root)
    {
        if (root == null) return;
        float lvl = ArgFloat("-assist", 0f);
        string spec = ArgString("-pushAt", null);
        if (lvl <= 0f && string.IsNullOrEmpty(spec)) return;
        BalanceAssist a = root.GetComponent<BalanceAssist>();
        if (a == null) a = root.AddComponent<BalanceAssist>();
        a.level = Mathf.Max(0f, lvl);
        a.pushSpec = spec;
        a.bound = false;
    }

    private void Awake()
    {
        forceMaxBw = ArgFloat("-assistFmax", forceMaxBw);
        gain = ArgFloat("-assistGain", gain);
        edgeInset = ArgFloat("-assistInset", edgeInset);
        bandTau = ArgFloat("-assistBandTau", bandTau);
        slewTime = ArgFloat("-assistSlew", slewTime);
        budgetNs = ArgFloat("-assistBudget", budgetNs);
        refillSeconds = ArgFloat("-assistRefill", refillSeconds);
        lostDistance = ArgFloat("-assistLost", lostDistance);
        walkLead = ArgFloat("-assistWalkLead", walkLead);
        ankleScale = ArgFloat("-assistAnkle", ankleScale);
        ankleCrouchScale = ArgFloat("-assistAnkleCrouch", ankleCrouchScale);
        centerK = ArgFloat("-assistCenter", centerK);
        centerMaxBw = ArgFloat("-assistCenterMax", centerMaxBw);
        holdK = ArgFloat("-assistHoldK", holdK);
        holdMaxBw = ArgFloat("-assistHoldMax", holdMaxBw);
        centerFree = ArgFloat("-assistCenterFree", 0f) > 0f;
        holdTail = ArgFloat("-assistHoldTail", holdTail);
        holdKeep = ArgFloat("-assistHoldKeep", holdKeep);
        standUpBw = Mathf.Clamp(ArgFloat("-assistStandUp", standUpBw), 0f, 0.10f);
        standUpIn = Mathf.Max(0.05f, ArgFloat("-assistStandUpIn", standUpIn));
        standUpHold = Mathf.Max(0f, ArgFloat("-assistStandUpHold", standUpHold));
        walkDirByIntent = ArgFloat("-assistWalkDir", 0f) > 0f;
        walkStartBw = ArgFloat("-assistWalkStart", walkStartBw);
        walkStartSec = ArgFloat("-assistWalkStartSec", walkStartSec);
        walkShiftBw = ArgFloat("-assistWalkShift", walkShiftBw);
        walkShiftMin = ArgFloat("-assistWalkShiftMin", walkShiftMin);
        homeK = ArgFloat("-assistHomeK", homeK);
        homeMaxBw = ArgFloat("-assistHomeMax", homeMaxBw);
        settleAsWalk = ArgFloat("-assistSettleWalk", 0f) > 0f;
        brakeBw = ArgFloat("-assistBrake", brakeBw);
        brakeSpeed = ArgFloat("-assistBrakeSpeed", brakeSpeed);
        brakeFloor = ArgFloat("-assistBrakeFloor", brakeFloor);
        settleDamp = Mathf.Clamp(ArgFloat("-assistSettleDamp", settleDamp), 0f, 10f);
        settleDampMax = Mathf.Clamp(ArgFloat("-assistSettleDampMax", settleDampMax), 0f, 0.5f);
        settleDampTail = Mathf.Clamp(ArgFloat("-assistSettleDampTail", settleDampTail), 0f, 3f);
        centerOneK = ArgFloat("-assistCenterOne", centerOneK);
        centerOneArm = ArgFloat("-assistCenterOneArm", centerOneArm);
        tiltFade0 = ArgFloat("-assistTilt0", tiltFade0);
        tiltFade1 = ArgFloat("-assistTilt1", tiltFade1);
        budget = budgetNs;
    }

    private void Bind()
    {
        Human human = GetComponent<Human>();
        state = human != null ? human.bodyStateEstimator : GetComponent<BodyStateEstimator>();
        balance = GetComponent<BalanceController>();
        intent = GetComponent<MotionIntent>();
        pose = GetComponent<PoseDriveController>();
        if (human != null) footLength = human.footSize.x;
        List<Rigidbody2D> list = new List<Rigidbody2D>();
        totalMass = 0f;
        foreach (HumanSegment seg in GetComponentsInChildren<HumanSegment>())
        {
            Rigidbody2D rb = seg.rb != null ? seg.rb : seg.GetComponent<Rigidbody2D>();
            if (rb == null || rb.bodyType != RigidbodyType2D.Dynamic) continue;
            list.Add(rb);
            totalMass += rb.mass;
        }
        bodies = list.ToArray();
        Transform t = transform.Find("Torso");
        torso = t != null ? t.GetComponent<Rigidbody2D>() : null;
        // While the assist holds the CoM, the ankle loop only needs to shape the stance; a lower
        // gain keeps its bang-bang command from chattering against the assist.
        if (!ankleScaled && balance != null && level > 0f
            && (Mathf.Abs(ankleScale - 1f) > 1e-6f || Mathf.Abs(ankleCrouchScale - 1f) > 1e-6f))
        {
            ankleP0 = balance.ankleComP;
            ankleD0 = balance.ankleComD;
            ankleScaled = true;
            ApplyAnkleScale();
        }
        ParsePushes();
        OpenLog();
        bound = state != null && torso != null && bodies.Length > 0 && totalMass > 0f;
    }

    private void FixedUpdate()
    {
        if (!bound || BodiesLost()) Bind();
        if (!bound) return;
        float dt = Time.fixedDeltaTime > 0f ? Time.fixedDeltaTime : 0.005f;
        clock += dt;

        pushNow = 0f;
        while (nextPush < pushes.Count && clock >= pushes[nextPush].x)
        {
            float j = pushes[nextPush].y;
            torso.AddForce(new Vector2(j, 0f), ForceMode2D.Impulse);
            pushNow += j;
            nextPush++;
        }

        if (ankleScaled) ApplyAnkleScale();
        float g = Mathf.Max(0.1f, -Physics2D.gravity.y);
        Vector2 com = state.comPosition;
        Vector2 vel = state.comVelocity;
        bool lDown = state.leftFootGrounded;
        bool rDown = state.rightFootGrounded;
        bool anyDown = lDown || rDown;
        airTimer = anyDown ? 0f : airTimer + dt;
        float footY;
        if (lDown && rDown) footY = Mathf.Min(state.leftFootPosition.y, state.rightFootPosition.y);
        else if (lDown) footY = state.leftFootPosition.y;
        else if (rDown) footY = state.rightFootPosition.y;
        else footY = Mathf.Min(state.leftFootPosition.y, state.rightFootPosition.y);
        float h = Mathf.Max(0.3f, com.y - footY);
        float w0 = Mathf.Sqrt(g / h);
        capturePointX = com.x + vel.x / w0;

        // Sole band, low-passed so a foot flickering in and out of contact does not kick the force.
        float a = state.supportMinX;
        float b = state.supportMaxX;
        if (!bandSeeded || !anyDown)
        {
            if (anyDown || !bandSeeded) { bandLo = a; bandHi = b; }
            bandSeeded = anyDown;
        }
        else
        {
            float k = Mathf.Clamp01(dt / Mathf.Max(dt, bandTau));
            bandLo += (a - bandLo) * k;
            bandHi += (b - bandHi) * k;
        }
        float inset = Mathf.Min(edgeInset, 0.25f * Mathf.Max(0f, bandHi - bandLo));
        bandMin = bandLo + inset;
        bandMax = bandHi - inset;
        bool walking = balance != null
            && balance.locomotionState == BalanceController.LocomotionState.Walking;
        // The stop's arrest step is still a walking step: the foot is on its way to the capture point,
        // so the standing spring and the standing front edge must not shove the body back meanwhile.
        if (settleAsWalk && pose != null && pose.ClSettleStepping) walking = true;
        if (intent == null && (walkDirByIntent || walkStartBw > 0f)) intent = GetComponent<MotionIntent>();
        float walkDir = vel.x >= 0f ? 1f : -1f;
        if (walkDirByIntent && intent != null && Mathf.Abs(intent.moveX) > 0.05f)
            walkDir = Mathf.Sign(intent.moveX);
        if (walking)
        {
            if (walkDir > 0f) bandMax += walkLead;
            else bandMin -= walkLead;
        }
        if (capturePointX > bandMax) excess = capturePointX - bandMax;
        else if (capturePointX < bandMin) excess = capturePointX - bandMin;
        else excess = 0f;

        float target = airTimer > airborneSeconds ? 0f : 1f;
        target *= Smooth01(budget / Mathf.Max(1e-3f, 0.3f * budgetNs));
        target *= 1f - Smooth01((Mathf.Abs(excess) - 0.5f * lostDistance) / Mathf.Max(0.01f, 0.5f * lostDistance));
        float tilt = Mathf.Abs(Mathf.DeltaAngle(0f, torso.rotation));
        target *= 1f - Smooth01((tilt - tiltFade0) / Mathf.Max(1f, tiltFade1 - tiltFade0));
        tone = Mathf.MoveTowards(tone, target, (target > tone ? toneRiseRate : toneFallRate) * dt);

        float fmax = Mathf.Max(0f, forceMaxBw) * totalMass * g * level;
        float desired = -Mathf.Clamp(gain * totalMass * w0 * w0 * excess, -fmax, fmax) * tone;
        crouchNow = balance != null ? Mathf.Clamp01(balance.crouchLevel) : 0f;
        holdLevel = holdTail > 0f && !walking ? Mathf.Max(crouchNow, holdLevel - dt / holdTail) : crouchNow;
        if (holdKeep > 0f)
        {
            if (walking)
            {
                holdKeepLevel = 0f;
                holdKeepTimer = 0f;
            }
            else if (crouchNow > 0.01f)
            {
                holdKeepLevel = Mathf.Max(holdKeepLevel, crouchNow);
                holdKeepTimer = holdKeep;
            }
            else if (holdKeepTimer > 0f)
            {
                holdKeepTimer -= dt;
            }
            else
            {
                holdKeepLevel = Mathf.Max(0f, holdKeepLevel - dt / 0.5f);
            }
            holdLevel = Mathf.Max(holdLevel, holdKeepLevel);
        }
        float kc = centerK;
        float cBw = centerMaxBw;
        if (holdK > 0f && holdLevel > 0f)
        {
            kc = Mathf.Lerp(centerK, holdK, holdLevel);
            cBw = Mathf.Lerp(centerMaxBw, holdMaxBw, holdLevel);
        }
        centerPart = 0f;
        bool both = lDown && rDown;
        bool one = centerOneK > 0f && anyDown && !both;
        if (centerOneK > 0f)
        {
            if (walking || !anyDown || crouchNow > 0.01f)
            {
                oneFootTimer = 0f;
                oneHomeOn = false;
            }
            else if (one)
            {
                // The band is this foot alone here, so its middle is the foot's middle.
                oneFootTimer += dt;
                if (oneFootTimer >= centerOneArm)
                {
                    oneHomeOn = true;
                    oneHomeX = 0.5f * (bandLo + bandHi);
                }
            }
            else if (!oneHomeOn)
            {
                // A brief touch of the other foot only leaks the clock, it does not restart it.
                oneFootTimer = Mathf.Max(0f, oneFootTimer - 2f * dt);
            }
        }
        oneMode = one || (oneHomeOn && both);
        if (oneMode)
            kc = holdK > 0f && holdLevel > 0f ? Mathf.Lerp(centerOneK, holdK, holdLevel) : centerOneK;
        float homeX = pose != null ? pose.ClStanceHomeX : float.NaN;
        homeOn = !float.IsNaN(homeX) && homeK > 0f && !walking && anyDown;
        if (homeOn)
        {
            kc = Mathf.Max(kc, homeK);
            cBw = Mathf.Max(cBw, homeMaxBw);
        }
        if (kc > 0f && !walking && (both || oneMode || homeOn))
        {
            // Critically damped spring toward the middle of the (smoothed) soles.
            float mid = oneHomeOn ? oneHomeX : 0.5f * (bandLo + bandHi);
            if (homeOn) mid = homeX;
            float cmax = Mathf.Max(0f, cBw) * totalMass * g * level;
            float acc = -kc * (com.x - mid) - 2f * Mathf.Sqrt(kc) * vel.x;
            centerPart = Mathf.Clamp(totalMass * acc, -cmax, cmax) * tone;
            desired += centerPart;
            desired = Mathf.Clamp(desired, -fmax, fmax);
        }
        walkAge = walking ? walkAge + dt : 0f;
        startPart = 0f;
        if (walkStartBw > 0f && walking && walkAge < walkStartSec
            && intent != null && Mathf.Abs(intent.moveX) > 0.05f)
        {
            // Walk-start push in the commanded direction, fading with speed and with the window.
            float dirS = Mathf.Sign(intent.moveX);
            float fade = Mathf.Clamp01(1f - vel.x * dirS / 0.5f) * (1f - walkAge / Mathf.Max(dt, walkStartSec));
            startPart = dirS * walkStartBw * totalMass * g * level * fade * tone;
            desired = Mathf.Clamp(desired + startPart, -fmax, fmax);
        }
        shiftPart = 0f;
        if (walkShiftBw > 0f)
        {
            float dirW = intent != null && intent.moveX < -0.05f ? -1f : 1f;
            if (walking && walkAge <= dt)
            {
                // Entry frame: the front foot's middle, from the raw sole edge on the commanded side.
                shiftHomeX = dirW > 0f ? b - 0.5f * footLength : a + 0.5f * footLength;
                shiftOn = anyDown && (shiftHomeX - com.x) * dirW > walkShiftMin;
            }
            float gap = (shiftHomeX - com.x) * dirW;
            if (!walking || walkAge >= walkStartSec || gap <= 0f) shiftOn = false;
            if (shiftOn)
            {
                // Fades with speed (0 at 0.5 m/s) and over the last 0.05 m before the foot's middle.
                float fadeW = Mathf.Clamp01(1f - vel.x * dirW / 0.5f) * Mathf.Clamp01(gap / 0.05f);
                shiftPart = dirW * walkShiftBw * totalMass * g * level * fadeW * tone;
                desired = Mathf.Clamp(desired + shiftPart, -fmax, fmax);
                startPart += shiftPart;
            }
        }
        // Walk brake: while the walk command is being ramped down (0.05 < |moveX| < 0.95) the pose walk
        // keeps its pace, so the stop's arrest step meets a full-speed body. This holds the CoM speed
        // (low-passed over a step) to brakeSpeed * |moveX|, braking only. Budget-free.
        velLp += (vel.x - velLp) * dt / (0.4f + dt);
        brakePart = 0f;
        if (brakeBw > 0f && walking && intent != null)
        {
            float mx = Mathf.Abs(intent.moveX);
            if (mx > 0.05f && mx < 0.95f)
            {
                float dirB = Mathf.Sign(intent.moveX);
                float over = velLp * dirB - Mathf.Max(brakeFloor, brakeSpeed * mx);
                if (over > 0f)
                {
                    brakePart = -dirB * Mathf.Min(brakeBw * totalMass * g * level, 2f * totalMass * over) * tone;
                    desired = Mathf.Clamp(desired + brakePart, -fmax, fmax);
                }
            }
        }
        // Settle damper: under the settle latch the body counts as walking, so the centring spring and
        // the brake are both off and the arrest step has no shaped force. This sheds forward speed and
        // resists the backward drift; pure velocity term, no position term (the foot is still travelling).
        // Budget-free like the brake. The tail keeps it on for dampTail after the latch drops.
        dampPart = 0f;
        bool settleDampOn = settleDamp > 0f && pose != null && pose.ClSettleStepping;
        if (settleDampOn) dampTailAge = 0f; else dampTailAge += dt;
        float dampW = settleDampOn ? 1f : Mathf.Clamp01(1f - dampTailAge / Mathf.Max(dt, settleDampTail));
        if (settleDamp > 0f && anyDown && dampW > 0f)
        {
            float dcap = Mathf.Max(0f, settleDampMax) * totalMass * g * level;
            dampPart = Mathf.Clamp(-totalMass * settleDamp * vel.x, -dcap, dcap) * tone * dampW;
            desired = Mathf.Clamp(desired + dampPart, -fmax, fmax);
        }
        forceX = Mathf.MoveTowards(forceX, desired, fmax / Mathf.Max(dt, slewTime) * dt);
        if (forceX != 0f)
        {
            for (int i = 0; i < bodies.Length; i++)
            {
                Rigidbody2D rb = bodies[i];
                rb.AddForce(new Vector2(forceX * rb.mass / totalMass, 0f));
            }
        }
        // req-233: the post-walk stand lift - a bounded upward force on every segment (a uniform
        // acceleration, like the horizontal assist), armed by a walk, on while the walk key is up
        // and a foot is down; ramps in over standUpIn. 0 (default) = off, off = byte-identical.
        if (standUpBw > 0f)
        {
            if (walking) standUpArmed = true;
            if (intent == null) intent = GetComponent<MotionIntent>();
            bool keyUp = intent == null || Mathf.Abs(intent.moveX) <= 0.05f;
            bool on = standUpArmed && keyUp && anyDown && airTimer <= airborneSeconds;
            standUpAge = on ? standUpAge + dt : 0f;
            // req-240: -assistStandUpHold - once the stand is quiet (both feet down, |comVelX| < 0.05)
            // the clock runs; after standUpHold s of quiet stand the lift ramps out over 1 s; a new
            // walk re-arms it. 0 = no change.
            if (standUpHold > 0f)
            {
                if (walking)
                {
                    standUpHoldF = 1f;
                    standUpQuiet = 0f;
                    standUpQuietSeen = false;
                }
                else
                {
                    if (!standUpQuietSeen && on && lDown && rDown && Mathf.Abs(vel.x) < 0.05f)
                        standUpQuietSeen = true;
                    if (standUpQuietSeen)
                    {
                        standUpQuiet += dt;
                        if (standUpQuiet >= standUpHold)
                            standUpHoldF = Mathf.Max(0f, standUpHoldF - dt);
                    }
                }
            }
            standUpF = 0f;
            if (on)
            {
                standUpF = standUpBw * Mathf.Clamp01(standUpAge / Mathf.Max(0.05f, standUpIn))
                    * totalMass * g;
                if (standUpHold > 0f) standUpF *= standUpHoldF;
                if (standUpF > 0f)
                {
                    for (int i = 0; i < bodies.Length; i++)
                    {
                        Rigidbody2D rb = bodies[i];
                        rb.AddForce(new Vector2(0f, standUpF * rb.mass / totalMass));
                    }
                }
            }
            float standUpBwNow = standUpF / Mathf.Max(1e-6f, totalMass * g);
            standUpSamples.Add(standUpBwNow);
            if (on) standUpOnSamples.Add(standUpBwNow);
        }
        float refill = budgetNs / Mathf.Max(0.1f, refillSeconds);
        float drain = Mathf.Abs(forceX);
        if (centerFree) drain = Mathf.Max(0f, drain - Mathf.Abs(centerPart));
        drain = Mathf.Max(0f, drain - Mathf.Abs(shiftPart) - Mathf.Abs(brakePart) - Mathf.Abs(dampPart));
        budget = Mathf.Clamp(budget + (refill - drain) * dt, 0f, budgetNs);
        if (budget < budgetMin) budgetMin = budget;

        float load = Mathf.Abs(forceX) / (totalMass * g);
        loadSamples.Add(load);
        if (anyDown && tone < 0.5f) weakToneSeconds += dt;
        WriteRow(walking, anyDown, tilt, load);
    }

    // The pose drive reads these gains live in its ankle loop (order 0), so a change here takes
    // effect on the next physics step.
    private void ApplyAnkleScale()
    {
        float k = Mathf.Max(0f, ankleScale);
        if (balance.crouchLevel > 0.0001f) k *= Mathf.Max(0f, ankleCrouchScale);
        balance.ankleComP = ankleP0 * k;
        balance.ankleComD = ankleD0 * k;
    }

    private static float Smooth01(float x)
    {
        x = Mathf.Clamp01(x);
        return x * x * (3f - 2f * x);
    }

    private bool BodiesLost()
    {
        if (bodies == null || torso == null) return true;
        for (int i = 0; i < bodies.Length; i++)
            if (bodies[i] == null) return true;
        return false;
    }

    private void ParsePushes()
    {
        pushes.Clear();
        nextPush = 0;
        if (string.IsNullOrEmpty(pushSpec)) return;
        foreach (string item in pushSpec.Split(','))
        {
            string[] kv = item.Split(':');
            if (kv.Length != 2) continue;
            if (float.TryParse(kv[0].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float t)
                && float.TryParse(kv[1].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float j))
                pushes.Add(new Vector2(t, j));
        }
        pushes.Sort((p, q) => p.x.CompareTo(q.x));
        while (nextPush < pushes.Count && pushes[nextPush].x < clock) nextPush++;
    }

    private void OpenLog()
    {
        if (log != null) return;
        string outDir = ArgString("-out", null);
        string label = ArgString("-label", null);
        if (string.IsNullOrEmpty(outDir) || string.IsNullOrEmpty(label)) return;
        try
        {
            Directory.CreateDirectory(outDir);
            log = new StreamWriter(Path.Combine(outDir, label + "_assist.csv"), false, new UTF8Encoding(false));
            log.WriteLine("t;fixedTime;tone;forceX;loadBw;cpX;bandMin;bandMax;excess;comX;comVelX;walking;grounded;trunkTilt;push;budget;center;crouch;hold;start;one;home;brake;damp;symTrim;leadL;leadR;hipAnchorXL;hipAnchorXR;hipDynAimM"
                + (standUpHold > 0f ? ";standUp;holdF" : ""));
            summaryPath = Path.Combine(outDir, label + "_assist_summary.txt");
        }
        catch (IOException)
        {
            log = null;
        }
    }

    private void WriteRow(bool walking, bool grounded, float tilt, float load)
    {
        if (log == null) return;
        row.Length = 0;
        row.Append(F(clock)).Append(';').Append(F(Time.fixedTime)).Append(';')
            .Append(F(tone)).Append(';').Append(F(forceX)).Append(';').Append(F(load)).Append(';')
            .Append(F(capturePointX)).Append(';').Append(F(bandMin)).Append(';').Append(F(bandMax)).Append(';')
            .Append(F(excess)).Append(';').Append(F(state.comPosition.x)).Append(';')
            .Append(F(state.comVelocity.x)).Append(';').Append(walking ? '1' : '0').Append(';')
            .Append(grounded ? '1' : '0').Append(';').Append(F(tilt)).Append(';').Append(F(pushNow))
            .Append(';').Append(F(budget)).Append(';').Append(F(centerPart)).Append(';').Append(F(crouchNow))
            .Append(';').Append(F(holdLevel)).Append(';').Append(F(startPart))
            .Append(';').Append(oneMode ? '1' : '0').Append(';').Append(homeOn ? '1' : '0').Append(';').Append(F(brakePart))
            .Append(';').Append(F(dampPart))
            .Append(';').Append(F(pose != null ? pose.SymTrimM : 0f))
            .Append(';').Append(F(pose != null ? pose.SymLeadLM : 0f))
            .Append(';').Append(F(pose != null ? pose.SymLeadRM : 0f))
            .Append(';').Append(F(pose != null ? pose.HipAnchorXL : 0f))
            .Append(';').Append(F(pose != null ? pose.HipAnchorXR : 0f))
            .Append(';').Append(F(pose != null ? pose.HipDynAimM : 0f));
        if (standUpHold > 0f)
        {
            float gw = Mathf.Max(0.1f, -Physics2D.gravity.y);
            row.Append(';').Append(F(standUpF / Mathf.Max(1e-6f, totalMass * gw)))
                .Append(';').Append(F(standUpHoldF));
        }
        log.WriteLine(row.ToString());
    }

    private void CloseLog()
    {
        if (ankleScaled && balance != null)
        {
            balance.ankleComP = ankleP0;
            balance.ankleComD = ankleD0;
            ankleScaled = false;
        }
        if (log == null) return;
        try
        {
            log.Flush();
            log.Dispose();
            if (!string.IsNullOrEmpty(summaryPath))
            {
                float sum = 0f, max = 0f;
                for (int i = 0; i < loadSamples.Count; i++)
                {
                    sum += loadSamples[i];
                    if (loadSamples[i] > max) max = loadSamples[i];
                }
                List<float> sorted = new List<float>(loadSamples);
                sorted.Sort();
                float p95 = sorted.Count > 0 ? sorted[Mathf.Min(sorted.Count - 1, (int)(0.95f * sorted.Count))] : 0f;
                float mean = loadSamples.Count > 0 ? sum / loadSamples.Count : 0f;
                StringBuilder s = new StringBuilder();
                s.Append("level=").Append(F(level)).Append(" ankleScale=").Append(F(ankleScale))
                    .Append(" ankleCrouchScale=").Append(F(ankleCrouchScale)).Append(" gain=").Append(F(gain))
                    .Append(" center=").Append(F(centerK))
                    .Append(" steps=").Append(loadSamples.Count)
                    .Append(" loadBw_mean=").Append(F(mean)).Append(" loadBw_p95=").Append(F(p95))
                    .Append(" loadBw_max=").Append(F(max)).Append(" weakToneGroundedSeconds=")
                    .Append(F(weakToneSeconds)).Append(" budgetMin=")
                    .Append(F(budgetMin == float.MaxValue ? budgetNs : budgetMin))
                    .Append(" pushes=").Append(nextPush).Append('/').Append(pushes.Count)
                    .Append(" holdK=").Append(F(holdK)).Append(" holdMax=").Append(F(holdMaxBw))
                    .Append(" centerFree=").Append(centerFree ? '1' : '0')
                    .Append(" holdTail=").Append(F(holdTail))
                    .Append(" holdKeep=").Append(F(holdKeep)).Append(" walkDir=").Append(walkDirByIntent ? '1' : '0')
                    .Append(" walkStart=").Append(F(walkStartBw))
                    .Append(" centerOne=").Append(F(centerOneK))
                    .Append(" settleDamp=").Append(F(settleDamp)).Append(" settleDampMax=").Append(F(settleDampMax))
                    .Append(" settleDampTail=").Append(F(settleDampTail))
                    // req-226 dynamic hip anchor ledger (mode/amp/in/out/aim, work J, writes, gate 0).
                    .Append(" hipDynMode=").Append(pose != null ? pose.HipDynMode : 0)
                    .Append(" hipDynAmp=").Append(F(pose != null ? pose.HipDynAmp : 0f))
                    .Append(" hipDynIn=").Append(F(pose != null ? pose.HipDynIn : 0f))
                    .Append(" hipDynOut=").Append(F(pose != null ? pose.HipDynOut : 0f))
                    .Append(" hipDynAim=").Append(F(pose != null ? pose.HipDynAim : 0f))
                    .Append(" hipDynWorkJ=").Append(F(pose != null ? pose.HipDynWorkJ : 0f))
                    .Append(" hipDynWrites=").Append(pose != null ? pose.HipDynWrites : 0)
                    .Append(" hipDynMaxDHipDeg=").Append(F(pose != null ? pose.HipDynMaxDHipDeg : 0f));
                if (standUpBw > 0f)
                {
                    float sSum = 0f, sMax = 0f;
                    for (int i = 0; i < standUpSamples.Count; i++)
                    {
                        sSum += standUpSamples[i];
                        if (standUpSamples[i] > sMax) sMax = standUpSamples[i];
                    }
                    s.Append(" standUpBw=").Append(F(standUpBw))
                        .Append(" standUpMean=").Append(F(standUpSamples.Count > 0 ? sSum / standUpSamples.Count : 0f))
                        .Append(" standUpPeak=").Append(F(sMax));
                    if (standUpHold > 0f)
                    {
                        float oSum = 0f;
                        for (int i = 0; i < standUpOnSamples.Count; i++) oSum += standUpOnSamples[i];
                        s.Append(" standUpHold=").Append(F(standUpHold))
                            .Append(" standUpEndF=").Append(F(standUpHoldF))
                            .Append(" standUpStandMean=")
                            .Append(F(standUpOnSamples.Count > 0 ? oSum / standUpOnSamples.Count : 0f));
                    }
                }
                File.WriteAllText(summaryPath, s.ToString());
            }
        }
        catch (IOException)
        {
        }
        log = null;
    }

    private void OnDestroy() { CloseLog(); }
    private void OnApplicationQuit() { CloseLog(); }

    private static string F(float v) => v.ToString("0.#####", CultureInfo.InvariantCulture);

    private static float ArgFloat(string name, float fallback)
    {
        string[] args = LauncherArgs.Args();
        int i = System.Array.IndexOf(args, name);
        if (i < 0 || i + 1 >= args.Length) return fallback;
        return float.TryParse(args[i + 1], NumberStyles.Float, CultureInfo.InvariantCulture, out float v)
            ? v : fallback;
    }

    private static string ArgString(string name, string fallback)
    {
        string[] args = LauncherArgs.Args();
        int i = System.Array.IndexOf(args, name);
        if (i < 0 || i + 1 >= args.Length) return fallback;
        return args[i + 1];
    }
}
