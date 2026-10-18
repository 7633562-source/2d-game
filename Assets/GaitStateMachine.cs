using UnityEngine;

// Capture-walk phase. Sole owner of walkPhase, state, stanceFoot, swingFoot.
// Inputs are contact events and xi. Output is the phase the drive should
// track. No Rigidbody2D, no collider, no Muscle: the parent freezes the
// footstep plan and writes joint targets.
public enum CaptureWalkState
{
    DoubleSupport = 0,
    Swing = 1,
    TouchdownBlend = 2,
    RecoveryStep = 3
}

public sealed class GaitStateMachine
{
    public struct Timing
    {
        public float doubleSupportMin;
        public float doubleSupportMax;
        public float doubleSupportShare;
        public float doubleSupportHold;
        public int touchdownFrames;
        public float touchdownBlendSeconds;
        public float recoveryMargin;
        public float swingOvertime;
        public float phaseWatchdog;
    }

    public struct World
    {
        public float dt;
        public float xi;
        public float comX;
        public float dir;
        public float stanceAnkleX;
        public float swingAnkleX;
        public float supportRadius;
        public float catchPast;
        public float share;
        public float launchComX;
        public float comLead;
        public bool collapsed;
        public bool posed;
        public bool suppressEmergency;
        public bool planValid;
        public bool planRecovery;
        public float planTargetX;
        public float planStartX;
        public float planDuration;
        public int planSwingSide;
    }

    public struct Contacts
    {
        public bool leftGrounded;
        public bool rightGrounded;
        public bool leftHeel;
        public bool rightHeel;
        public bool leftTouching;
        public bool rightTouching;
        public bool leftLoaded;
        public bool rightLoaded;
        public bool leftSupporting;
        public bool rightSupporting;
    }

    public struct Decision
    {
        public bool freezePlan;
        public bool emergency;
        public bool watchdog;
        public bool enter;
        public CaptureWalkState next;
        public int swingSide;
        public bool swapRoles;
        public int newStanceSign;
    }

    public CaptureWalkState State { get; private set; }
    public int WalkPhase => (int)State;
    public int StanceFoot { get; private set; } = 1;
    public int SwingFoot => -StanceFoot;
    public float StateAge { get; private set; }
    public float SwingAge { get; private set; }
    // Time since the swing sole actually left. The airborne curve starts
    // here, not at freeze: freeze age includes the glued peel, and feeding
    // that into X is a shuffle jump the moment the probe drops.
    public float SwingAirAge { get; private set; }
    public bool SwingLeftGround { get; private set; }
    public int ContactFrames { get; private set; }
    public int AbandonStreak { get; private set; }
    public int StepCount { get; private set; }
    public float BothDownAge { get; private set; }
    public int BothAirSteps { get; private set; }
    public int WatchdogCount { get; private set; }
    public Contacts LastContacts { get; private set; }
    private bool rollLatched;

    // ─── WALK-1 instrumentation: evidence only, nothing below reads it ───
    // Why the last phase entry fired and what the first-step gates said. The
    // recorder publishes these per frame; the state machine's own behaviour
    // does not depend on any of them.
    public const int ReasonNone = 0;
    public const int ReasonContact = 1;
    public const int ReasonClock = 2;
    public const int ReasonLead = 3;
    public const int ReasonWatchdog = 4;
    public const int ReasonEmergency = 5;
    public const int ReasonLate = 6;

    // Role-swap mode: 1 = paired (both soles down for doubleSupportHold),
    // 2 = catchOwns (the landing sole is the only support).
    public const int SwapModeNone = 0;
    public const int SwapModePaired = 1;
    public const int SwapModeCatchOwns = 2;

    // First-step gate bits, one bit per gate DecideDoubleSupport tests:
    // 1 clock (StateAge >= doubleSupportMin), 2 trail heel up, 4 posed,
    // 8 !collapsed, 16 suppressEmergency, 32 paired (roll/first/stacked),
    // 64 canStep. The mask says which gates passed this frame.
    public const int GateClock = 1;
    public const int GateTrailUp = 2;
    public const int GatePosed = 4;
    public const int GateNotCollapsed = 8;
    public const int GateSuppress = 16;
    public const int GatePaired = 32;
    public const int GateCanStep = 64;

    public int LastEntryReason { get; private set; }
    public float LastEntryStateAge { get; private set; }
    public int LastSwapReason { get; private set; }
    public int LastSwapMode { get; private set; }
    public float LastSwapStateAge { get; private set; }
    public int LastGateMask { get; private set; }
    public int LastRejectCode { get; private set; }

    public bool IsDualSupport =>
        State == CaptureWalkState.DoubleSupport
        || State == CaptureWalkState.TouchdownBlend;

    public bool IsSingleSupport =>
        State == CaptureWalkState.Swing
        || State == CaptureWalkState.RecoveryStep;

    public bool IsStanceLeft => StanceFoot < 0;

    public bool IsStanceFoot(bool left) => left == (StanceFoot < 0);

    public bool IsSwingFoot(bool left) => !IsStanceFoot(left);

    // Role, not contact: both feet may carry in dual support, stanceFoot
    // only in Swing / Recovery. Drive uses this with ContactSensor.touching.
    public bool MayCarry(bool left) => IsDualSupport || IsStanceFoot(left);

    public void ResetPhase()
    {
        State = CaptureWalkState.DoubleSupport;
        StateAge = 0f;
        SwingAge = 0f;
        SwingAirAge = 0f;
        SwingLeftGround = false;
        ContactFrames = 0;
        AbandonStreak = 0;
        BothDownAge = 0f;
        BothAirSteps = 0;
        WatchdogCount = 0;
        rollLatched = false;
        StepCount = 0;
    }

    public void Begin(int stanceSign)
    {
        ResetPhase();
        StepCount = 0;
        EnterDoubleSupport(stanceSign);
    }

    public void EnterDoubleSupport(int stanceSign)
    {
        StanceFoot = stanceSign >= 0 ? 1 : -1;
        SwingAge = 0f;
        SwingAirAge = 0f;
        Enter(CaptureWalkState.DoubleSupport);
    }

    public void NotifyPlanFrozen(bool gluedReplan)
    {
        if (!gluedReplan)
        {
            SwingAge = 0f;
            SwingAirAge = 0f;
        }
        ContactFrames = 0;
    }

    public Decision Decide(Timing timing, World world, Contacts contacts)
    {
        float dt = Mathf.Max(0f, world.dt);
        StateAge += dt;
        LastContacts = contacts;
        // WALK-1: the reason is per-frame evidence, so it is cleared here.
        LastEntryReason = ReasonNone;
        LastGateMask = 0;
        LastRejectCode = 0;

        bool stanceLeft = StanceFoot < 0;
        bool swingTouching = Touching(contacts, !stanceLeft);
        bool stanceTouching = Touching(contacts, stanceLeft);
        bool swingHeel = Heel(contacts, !stanceLeft);
        bool stanceCarry = MayCarry(stanceLeft) && stanceTouching;
        bool swingCarry = MayCarry(!stanceLeft) && swingTouching;
        if (IsDualSupport && stanceTouching && swingTouching)
            BothDownAge += dt;
        else
            BothDownAge = 0f;
        if (!stanceCarry && !swingCarry)
            BothAirSteps++;

        Decision decision = default;
        if (TickWatchdog(timing, world, ref decision))
            return decision;

        switch (State)
        {
            case CaptureWalkState.DoubleSupport:
                DecideDoubleSupport(timing, world, swingHeel, ref decision);
                break;
            case CaptureWalkState.Swing:
            case CaptureWalkState.RecoveryStep:
                DecideSwing(timing, world, swingTouching, ref decision);
                break;
            case CaptureWalkState.TouchdownBlend:
                DecideTouchdown(timing, world, contacts, ref decision);
                break;
        }
        return decision;
    }

    public void Apply(Decision decision)
    {
        if (!decision.enter) return;
        // WALK-1: latch why the phase changed, and why the last swap fired.
        LastEntryStateAge = StateAge;
        if (decision.swapRoles)
        {
            StanceFoot = decision.newStanceSign >= 0 ? 1 : -1;
            StepCount++;
            LastSwapReason = LastEntryReason;
            LastSwapStateAge = StateAge;
            rollLatched = false;
        }
        Enter(decision.next);
    }

    private void DecideDoubleSupport(
        Timing timing, World world, bool swingHeel, ref Decision decision)
    {
        float support = world.supportRadius;
        float past = (world.xi - world.stanceAnkleX) * world.dir;
        // Emergency is xi leaving the plate (toe / heel), not 8 cm past
        // the ankle. After a good plant the orbit already has xi ~8 cm
        // ahead of the new stance — that is residual speed, not a fall.
        // catchPast 0.08 stole DS into Recovery in 5 ms (capwalk16) and
        // the 120° squat never left the slab. Parent now passes 0 so
        // aheadLimit is the toe; keep the same fallback if a probe
        // restores catchPast.
        float aheadLimit = world.catchPast > 0.001f
            ? Mathf.Max(world.catchPast, support)
            : support;
        // A brand-new stance is already behind live xi. Do not re-open
        // Recovery on the first frames of that DS; the periodic freeze
        // below is the next step.
        bool holdCatch = StepCount > 0
                         && StateAge < Mathf.Max(0.02f, timing.doubleSupportMin);
        bool emergency = !world.suppressEmergency && !holdCatch
                         && (past > aheadLimit || past < -support);
        float need = Mathf.Clamp01(timing.doubleSupportShare);
        // Share chatter around the 0.62 line froze a new swing every
        // minHold (capwalk5: 29 swaps). Latch on, drop 0.18 below to off.
        if (world.share >= need) rollLatched = true;
        else if (world.share < Mathf.Max(0.08f, need - 0.18f)) rollLatched = false;
        bool rolled = rollLatched;
        float minHold = Mathf.Max(0f, timing.doubleSupportMin);
        float maxHold = Mathf.Max(0.05f, timing.doubleSupportMax);
        bool stacked = Mathf.Abs((world.stanceAnkleX - world.swingAnkleX) * world.dir)
                       < 0.04f;
        // Dirt lift finishes in DoubleSupport: the parent peels ONE
        // trail with a caudal rocker after onset lean. Stacked share is
        // 0.5 so roll never latches. Lean tests (torso −14°, comX 0.069 m
        // / supportMargin 0.14 every run) proved the ankle loop holds
        // CoM inside the two-pin polygon — share and xiPastSupport
        // therefore never become true, and waiting for them is the
        // DualSupport freeze (burn_walk 40 s / swap 0). A travel latch
        // without peel Zeroed the planted swing and dumped at 6.300 s.
        // First freeze is posed (onset lean clock in the parent), not
        // CoM travel. Swing keeps in-place lift IK while the sole still
        // touches, so this is not that Zero. Later cycles still wait
        // for trailUp — share-roll with the rear planted is the dump.
        // BothDownAge cannot gate this freeze — it is identically 0
        // the frame the trail leaves. Landing handover still uses it
        // in DecideTouchdown.
        // Caudal heel pin, not the filled heel AABB and not heel-OR-toe.
        // The heel box is 70 % of the sole: 12° plantarflex never emptied
        // estimator Heel (burn_walk swingFoot 0.955 / walkSwap 0). A
        // remaining toe scrape is clearance. Landing still uses union
        // touching in DecideSwing / DecideTouchdown.
        bool trailUp = !swingHeel;
        bool xiPastSupport = past > support;
        bool firstAir = StepCount == 0 && StateAge >= minHold && trailUp;
        bool firstPosed = StepCount == 0 && StateAge >= minHold && world.posed;
        bool rolledAir = rolled && trailUp;
        bool stackedAir = stacked && trailUp
                          && ((StepCount > 0 && StateAge >= maxHold)
                              || (StepCount == 0 && xiPastSupport));
        bool ready = StateAge >= minHold
                     && (rolledAir || firstAir || stackedAir || firstPosed);
        bool paired = rolledAir || firstAir || stackedAir || firstPosed;
        // After a landing the pair can fold with xi still between the
        // ankles: collapsed is true and emergency is false, so the
        // periodic second step never froze and the watchdog sat in
        // Recovery face-down. A first DoubleSupport (StepCount 0) still
        // refuses a collapsed start — that is the quiet-stance squat.
        // Emergency already bypasses collapsed.
        bool canStep = ready && world.posed
                       && (!world.collapsed || StepCount > 0);
        // WALK-1: publish the gate state of the step that is (not) opening.
        int gateMask = 0;
        if (StateAge >= minHold) gateMask |= GateClock;
        if (trailUp) gateMask |= GateTrailUp;
        if (world.posed) gateMask |= GatePosed;
        if (!world.collapsed) gateMask |= GateNotCollapsed;
        if (world.suppressEmergency) gateMask |= GateSuppress;
        if (paired) gateMask |= GatePaired;
        if (canStep) gateMask |= GateCanStep;
        LastGateMask = gateMask;
        // CoM past the lead toe is statically unsupported — that is the
        // 6.3 s transfer stall (c125adbd / burn_walk onset +1.30 s).
        // Label 6.300 = BalanceController.UpdateState fallCoMOffset 0.30
        // after DualSupport never peeled (swap 0) or after Recovery chair
        // (burn_walk plant 5.900 → RecoveryStep 5.905 → GETUP 5.975).
        // Prior gate StepCount>0 && !suppressEmergency left first DS
        // stacked under suppressEmergency: posed needs !trailHeel, so
        // Swing never froze and |offset| hit 0.30. Overdue heel-pin
        // transfer must open Swing even on StepCount 0 / suppress —
        // Recovery only once the trail heel is already up.
        bool comPastToe = (world.comX - world.stanceAnkleX) * world.dir
                          > support;
        bool overdueComPastToe = comPastToe
                                 && StateAge >= minHold;
        if (overdueComPastToe && swingHeel)
        {
            decision.freezePlan = true;
            decision.enter = true;
            decision.emergency = false;
            decision.next = CaptureWalkState.Swing;
            decision.swingSide = -StanceFoot;
            LastEntryReason = ReasonLead;
        }
        else if (overdueComPastToe
                 && StepCount > 0
                 && !world.suppressEmergency
                 && !swingHeel)
        {
            decision.freezePlan = true;
            decision.enter = true;
            decision.emergency = true;
            decision.next = CaptureWalkState.RecoveryStep;
            decision.swingSide = -StanceFoot;
            LastEntryReason = ReasonLead;
        }
        else if (paired && canStep)
        {
            decision.freezePlan = true;
            decision.emergency = false;
            decision.enter = true;
            decision.next = CaptureWalkState.Swing;
            decision.swingSide = -StanceFoot;
            // Which of the four paired terms opened this step.
            LastEntryReason = firstAir ? ReasonContact
                : firstPosed ? ReasonClock
                : rolledAir ? ReasonLead
                : ReasonLead;
        }
        else if (emergency && trailUp)
        {
            decision.freezePlan = true;
            decision.emergency = true;
            decision.enter = true;
            decision.next = CaptureWalkState.RecoveryStep;
            decision.swingSide = -StanceFoot;
            LastEntryReason = ReasonEmergency;
        }
        else
        {
            // WALK-1: the controller's own reason for NOT opening a step.
            LastRejectCode = StateAge < minHold ? 1
                : !trailUp ? 2
                : !world.posed ? 3
                : world.collapsed && StepCount == 0 ? 4
                : !paired && !overdueComPastToe ? 5
                : 6;
        }
    }

    private void DecideSwing(
        Timing timing, World world, bool swingTouching, ref Decision decision)
    {
        SwingAge += world.dt;
        if (!swingTouching)
        {
            if (!SwingLeftGround)
                SwingAirAge = 0f;
            SwingLeftGround = true;
            SwingAirAge += world.dt;
        }
        if (swingTouching)
            ContactFrames++;
        else
            ContactFrames = 0;
        if (SwingLeftGround
            && ContactFrames >= Mathf.Max(1, timing.touchdownFrames))
        {
            float plantMiss = world.planValid
                ? (world.xi - world.planTargetX) * world.dir
                : 0f;
            float plantMargin = Mathf.Max(0.005f, timing.recoveryMargin);
            // Live xi grows e^{ωt} about the stance toe while the
            // frozen target sits at freeze-time xi(T). plantMiss then
            // vetoes a sole that is already on the slab,
            // TouchdownBlend never runs, and the trunk rotates over
            // the stance (auto1343 / 6.300 s; p_auto 17:32 second
            // Swing 5.680 → late 6.095). Residual speed past that
            // freeze point is the gait on every periodic step, not
            // only StepCount 0. Recovery plans still require the
            // catch to land on xi.
            bool periodicCatch = !world.planRecovery;
            if (plantMiss <= plantMargin || periodicCatch)
            {
                decision.enter = true;
                decision.next = CaptureWalkState.TouchdownBlend;
                LastEntryReason = ReasonContact;
                return;
            }
        }
        // Recovery that never left the slab: the catch X still arrived.
        // Waiting for SwingLeftGround kept walkSwap at 2 (capwalk16).
        if (world.planRecovery && world.planValid && swingTouching
            && StateAge > 0.05f)
        {
            float stride = (world.planTargetX - world.planStartX) * world.dir;
            float missX = (world.swingAnkleX - world.planTargetX) * world.dir;
            if (stride > 0.04f && Mathf.Abs(missX) < 0.04f)
            {
                decision.enter = true;
                decision.next = CaptureWalkState.TouchdownBlend;
                LastEntryReason = ReasonContact;
                return;
            }
        }

        float miss = world.planValid
            ? (world.xi - world.planTargetX) * world.dir
            : 0f;
        float lostMargin = Mathf.Max(0.005f, timing.recoveryMargin);
        // StateAge resets on every Enter, including re-entering Recovery, so
        // this is a 0.10 s re-aim clock. SwingAge does not reset on a glued
        // re-plan and would freeze every step. lostFwd used to ignore an
        // already-Recovery plan: the catch target froze once, xi ran away,
        // and the second catch never existed (capwalk11 = capwalk10).
        // First periodic swing from a stacked DualSupport: freeze-time
        // hip span is ~0.17 m while the 9° bow stores ~0.13 m of inverted-
        // pendulum x0 that is not yet in com.x (two pins). PlaceFootX
        // then ClampToReach parks the sole at that span, and at StateAge
        // 0.10 s live xi has already run past it OR the plant still sits
        // ahead of freeze-xi. Either way RecoveryStep of a still-reaching
        // catch is the 120° squat (burn_cap swap 1, fallCoMOffset 0.30 at
        // 6.275 s).
        //
        // burn_walk 15:10: that same 0.10 s clock is the 6.300 s dump
        // AFTER the first plant. First catch (StepCount 0) already
        // skipped lostFwd; swap increments StepCount, the next Swing
        // is "later" and StateAge > 0.10 sees live xi past freeze-xi.
        // That miss is residual orbit (e^{ωt} about the new stance),
        // not a missed plant — RecoveryStep 120° then
        // UpdateState(fallCoMOffset 0.30) at walkTime 5 + 1.30 s,
        // walkSwapCount stuck at 1, torso 90°. Periodic plans stay
        // Swing until TouchdownBlend; only an emergency recovery plan
        // may abandon on miss. Late of a glued peel is the chair;
        // late of an airborne periodic swing re-freezes cruise from
        // live xi (p_auto 17:32: second Swing 5.680, late 6.095).
        bool lostFwd = world.planRecovery
                       && world.planValid
                       && StateAge > 0.10f
                       && miss > lostMargin;
        bool lostBack = world.planRecovery
                        && world.planValid
                        && StateAge > 0.10f
                        && miss < -lostMargin;
        // Periodic airborne tracks SwingAirAge (DriveCaptureSwingLeg).
        // SwingAge includes the glued peel (~35 ms: freeze 5.805, air
        // 5.840). That peel spent overtime, so late fired while the
        // curve was still in landY-press: p_auto 19:42 SwingAge 0.415
        // > 0.26+0.15 opened RecoveryStep at 6.220, air age 0.380,
        // getUpFallTime 6.225, swap stuck at 2. Recovery plans still
        // use freeze age (catchSlide travels X while glued).
        float overtime = Mathf.Max(0f, timing.swingOvertime);
        float lateAge = (!world.planRecovery && SwingLeftGround)
            ? SwingAirAge
            : SwingAge;
        bool late = lateAge > world.planDuration + overtime;
        // Glued peel (heel OR toe still down): T+overtime Recovery is
        // the 120° chair (burn_walk 6.300). Keep peeling until air.
        bool gluedPeriodic = !world.planRecovery && !SwingLeftGround;
        if (gluedPeriodic)
            late = false;
        // Late airborne periodic: press past T is the plant, not a
        // missed catch. CoM past the stance toe is the third-step
        // orbit (~2 m/s after two swaps), not a reason to Recovery.
        // RecoveryStep of that still-reaching sole is the chair
        // (tilt 96°, pelvisDrop 0.87, GETUP fail). Keep the frozen
        // catch through one extra overtime of air; only then
        // re-freeze cruise from live xi. Never b*=0 here.
        if (!world.planRecovery && SwingLeftGround && late)
        {
            bool pressLost = SwingAirAge > world.planDuration + overtime * 2f;
            if (!pressLost)
                return;
            AbandonStreak++;
            decision.freezePlan = true;
            decision.emergency = false;
            decision.enter = true;
            decision.next = CaptureWalkState.Swing;
            decision.swingSide = world.planValid ? world.planSwingSide : -StanceFoot;
            LastEntryReason = ReasonLate;
            return;
        }
        if (late || lostFwd || lostBack)
        {
            AbandonStreak++;
            decision.freezePlan = true;
            decision.emergency = true;
            decision.enter = true;
            decision.next = CaptureWalkState.RecoveryStep;
            decision.swingSide = world.planValid ? world.planSwingSide : -StanceFoot;
            LastEntryReason = late ? ReasonLate : ReasonLead;
        }
    }

    private void DecideTouchdown(
        Timing timing, World world, Contacts contacts, ref Decision decision)
    {
        bool landingIsLeft = world.planSwingSide < 0;
        bool landingTouching = Touching(contacts, landingIsLeft);
        // Same as DecideSwing: a periodic sole that has arrived stays
        // on the slab. Bounce-back into Swing after the first swap
        // killed the second plant (p_auto 17:32 walkSwap stuck at 1).
        bool periodicCatch = !world.planRecovery;
        if (!landingTouching)
            ContactFrames++;
        else
            ContactFrames = 0;
        if (!periodicCatch
            && ContactFrames >= Mathf.Max(2, timing.touchdownFrames))
        {
            decision.enter = true;
            decision.next = world.planRecovery
                ? CaptureWalkState.RecoveryStep
                : CaptureWalkState.Swing;
            return;
        }

        float blendSeconds = Mathf.Max(0.01f, timing.touchdownBlendSeconds);
        float doubleHold = Mathf.Max(0f, timing.doubleSupportHold);
        bool bothDown = contacts.leftTouching && contacts.rightTouching;
        bool oldStanceTouching = Touching(contacts, !landingIsLeft);
        // Age-only paired swapped onto one plate while the old stance
        // was still in the air — DualSupport with no support. Wait for
        // BothDownAge: the new sole has arrived AND the old one still
        // carries, then swap. Happy-path first plant is both down.
        bool paired = BothDownAge >= doubleHold && bothDown;
        bool landingCarry = MayCarry(landingIsLeft) && landingTouching;
        // p_auto 15:22 (da8cbdfc): first catch planted, old stance
        // already off the slab, BothDownAge stayed 0, walkSwap 0.
        // Roles froze: swing sole 91 %, stance 3 % — CoP and plant IK
        // stayed on the airborne rear while the catch scraped, then
        // the trunk sank. Support has already moved when the landing
        // is the only plate; swap then. A still-touching pair still
        // waits the hold. The 0.30 DualSupport restore is the walk
        // agent's cutoff, not this handover.
        bool catchOwns = landingTouching
                         && !oldStanceTouching
                         && StateAge >= blendSeconds;
        if (StateAge >= blendSeconds && landingCarry && (paired || catchOwns))
        {
            decision.enter = true;
            decision.next = CaptureWalkState.DoubleSupport;
            decision.swapRoles = true;
            decision.newStanceSign = world.planSwingSide;
            // WALK-1: which support state the swap was made from.
            LastSwapMode = paired ? SwapModePaired : SwapModeCatchOwns;
            LastEntryReason = ReasonContact;
        }
    }

    private bool TickWatchdog(Timing timing, World world, ref Decision decision)
    {
        float limit = timing.phaseWatchdog;
        if (limit <= 0.0001f) return false;
        if (StateAge < limit) return false;
        // Dual support waiting for the CoM to roll is not a stuck plant.
        // The 0.5 s ceiling used to freeze Recovery of the swing role —
        // the trail that still holds the mass — which is the capwalk2 sit.
        // Emergency (xi past the sole) still freezes from DecideDoubleSupport.
        if (State == CaptureWalkState.DoubleSupport && !world.collapsed)
        {
            bool bothDown = LastContacts.leftTouching && LastContacts.rightTouching;
            float past = (world.xi - world.stanceAnkleX) * world.dir;
            float aheadLimit = world.catchPast > 0.001f
                ? world.catchPast
                : world.supportRadius;
            // Skip only while xi is still inside the lead. Once it has left
            // the ankle, a low share must not park the next RecoveryStep
            // (capwalk2 sit vs capwalk10 vault with swap 1).
            if (bothDown && world.share < Mathf.Clamp01(timing.doubleSupportShare)
                && past <= aheadLimit)
                return false;
        }
        // First DualSupport may freeze Swing while the swing sole is
        // still down only after a glued peel that already started.
        // Skip while that peel is on; one fold clock past the ceiling.
        // RecoveryStep keeps the ceiling.
        if (State == CaptureWalkState.Swing && !world.collapsed)
        {
            bool swingDown = Touching(LastContacts, StanceFoot >= 0);
            if (swingDown && !SwingLeftGround && StateAge < limit + 0.40f)
                return false;
        }
        // Periodic Swing / TouchdownBlend: 0.5 s Recovery of an
        // airborne or just-planted sole is the missed second plant
        // (p_auto 17:32, swap 1, GETUP 6.225). Stay until the sole
        // shares the slab, or the parent restores a chair. Late
        // re-freezes cruise; BodyCollapsed still Restores Plant.
        if ((State == CaptureWalkState.Swing
             || State == CaptureWalkState.TouchdownBlend)
            && !world.planRecovery
            && !world.collapsed)
            return false;
        WatchdogCount++;
        decision.freezePlan = true;
        decision.emergency = true;
        decision.watchdog = true;
        decision.enter = true;
        decision.next = CaptureWalkState.RecoveryStep;
        decision.swingSide = world.planValid ? world.planSwingSide : -StanceFoot;
        LastEntryReason = ReasonWatchdog;
        return true;
    }

    private void Enter(CaptureWalkState next)
    {
        if (next == CaptureWalkState.TouchdownBlend
            || next == CaptureWalkState.DoubleSupport)
            AbandonStreak = 0;
        if (next == CaptureWalkState.Swing
            || next == CaptureWalkState.RecoveryStep)
        {
            if (State != CaptureWalkState.Swing
                && State != CaptureWalkState.RecoveryStep)
            {
                SwingLeftGround = false;
                SwingAirAge = 0f;
            }
        }
        else
        {
            SwingLeftGround = false;
            SwingAirAge = 0f;
        }
        State = next;
        StateAge = 0f;
        ContactFrames = 0;
    }

    private static bool Touching(Contacts contacts, bool left)
    {
        return left ? contacts.leftTouching : contacts.rightTouching;
    }

    private static bool Heel(Contacts contacts, bool left)
    {
        return left ? contacts.leftHeel : contacts.rightHeel;
    }
}
