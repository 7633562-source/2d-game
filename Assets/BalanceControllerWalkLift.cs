using UnityEngine;

// WALK-1 overlay. Cadence is the legacy Stance split (standLeg like
// cold one-leg). Unload used to wait for PlantStandReady (xi past the
// stance toe) — a closed loop: CoM cannot leave the plate until the
// trail knee shortens, and that shorten never ran (walkPhase stuck at
// 0). Dirt Unload is that knee shorten on the first Stance frames;
// hip stays quiet on dirt (not a scissor). Clear IK and Advance stay
// aerial. EnterStance only after Advance plants that airborne foot.
// Closed: useWalkLift 0. CaptureWalk is -captureWalk 1, not this path.
// Quiet stance, crouch, the pushes, cold one-leg and fall-catch never
// enter.
public partial class BalanceController
{
    public enum WalkLiftPhase
    {
        Idle = 0,
        Unload = 1,
        Clear = 2,
        Advance = 3,
        Restore = 4
    }

    [Header("WALK-1 unload-shorten-clear (legacy walk only)")]
    [Tooltip("1 = Unload/Clear/Advance overlay on the Stance split. 0 = overlay off.")]
    public int useWalkLift = 1;
    [Tooltip("1 = Idle may Unload after PlantStandReady even if the probe still says grounded. HoldWalkLiftShorten on dirt; Clear IK stays aerial. 0 = former wait-for-air (swingFoot ~1).")]
    public float walkLiftUnloadFromDirt = 1f;
    [Tooltip("Stance share that must hold before UNLOAD, 0…1. Stacked feet use the CoM-on-stance gate instead.")]
    public float walkLiftLoadRatio = 0.80f;
    [Tooltip("Seconds the load gate must hold before UNLOAD.")]
    public float walkLiftLoadHold = 0.05f;
    [Tooltip("Seconds of stance before a stacked-foot first step may UNLOAD.")]
    public float walkLiftIdleSeconds = 0.10f;
    [Tooltip("UNLOAD duration: knee shortens, hip holds, stance extends, s.")]
    public float walkLiftUnloadSeconds = 0.12f;
    [Tooltip("CLEAR duration: vertical world-space foot track after lift-off, s. Stays on the Unload shorten until the sole leaves.")]
    public float walkLiftClearSeconds = 0.18f;
    [Tooltip("RESTORE duration: unwind the planted Clear shorten before a role swap, s. EnterStance on a 35°/0° statue (walklift_cadence 7.500) dumps.")]
    public float walkLiftRestoreSeconds = 0.30f;
    [Tooltip("ADVANCE duration: forward min-jerk to the landing X, s. 0.45 lets the inverted pendulum pass the stance toe before the swing arrives (same T CaptureWalk rejected). 0.32 plants sooner; xi(T) grows ~2.7× instead of ~4.1×.")]
    public float walkLiftAdvanceSeconds = 0.45f;
    [Tooltip("Extra swing-knee flexion during UNLOAD, deg (plus).")]
    public float walkLiftKneeFlex = 35f;
    [Tooltip("Extra hip flexion while UNLOAD holds the present angle, deg (minus on the joint).")]
    public float walkLiftHipHoldFlex = 5f;
    [Tooltip("Swing-ankle dorsiflex command during UNLOAD/CLEAR, 0…1. Not an ankle-angle PD.")]
    public float walkLiftAnkleDorsi = 0.40f;
    [Tooltip("CLEAR apex above the lift-start ankle, m.")]
    public float walkLiftClearHeight = 0.06f;
    [Tooltip("ADVANCE catch reach from the stance ankle, m. Plant is live CoM plus 3 cm, clamped here. Not the 0.55 m LIPM ceiling.")]
    public float walkLiftStepLength = 0.18f;
    [Tooltip("ADVANCE landing distance cap, m. Catch uses live CoM inside walkLiftStepLength; this 0.55 m LIPM ceiling is not the plant (walklift_air never touched).")]
    public float walkLiftStepMax = 0.55f;
    [Tooltip("Unused after walklift_air. Catch plants on live CoM, not e^{ωT} lookahead.")]
    public float walkLiftCaptureLookahead = 1f;
    [Tooltip("Cap on the walk ankle command that rolls the trunk forward (minus when facing +X). Floor is 0.30: 0.15 killed the gait before the clip could fire. Plantarflex (brake) is not cut.")]
    public float walkLiftAnkleFwdDriveMax = 0.30f;
    [Tooltip("Walk-only. Behind-error below this (m) still uses ankleComP=14. Quiet stance / one-leg never enter.")]
    public float walkAnkleDorsiHold = 0.04f;
    [Tooltip("Walk-only. At this behind-distance (m) the dorsiflex error fed to P is 0 and D fades with it. 0 = off. Capture plants are 0.18–0.30 m.")]
    public float walkAnkleDorsiFade = 0.16f;
    [Tooltip("Heel/toe clearance that confirms lift-off, m. Diagnostic, not a gate into CLEAR.")]
    public float walkLiftClearance = 0.025f;
    [Tooltip("After SwapCount>0 while both down: CoM-ankle fraction kept on the old strut (now labeled swing). 0 = walklift_restore (strut silenced, dump +X). 1 = walklift_spent0 (both full CoM, dump -X).")]
    public float walkLiftSharedSwingAnkle = 0.35f;
    [Tooltip("CoM setpoint behind the stance sole at CLEAR start, m.")]
    public float walkLiftComLeadStart = -0.01f;
    [Tooltip("CoM setpoint ahead of the stance sole at ADVANCE end, m.")]
    public float walkLiftComLeadEnd = 0.04f;
    [Tooltip("Stance-hip extension bias during UNLOAD/CLEAR (plus = extension). Routes the remaining knee reaction into the planted strut.")]
    public float walkLiftStanceHipExtend = 0.12f;
    [Tooltip("Abort UNLOAD/CLEAR/ADVANCE if the pelvis has dropped this far from the UNLOAD height, m.")]
    public float walkLiftAbortDrop = 0.08f;
    [Tooltip("Abort if |torso tilt| exceeds this, deg.")]
    public float walkLiftAbortTilt = 40f;
    [Tooltip("ADVANCE: seconds to min-jerk the swing-hip reference onto the IK. 0.12…0.20.")]
    public float walkLiftAdvanceHipRamp = 0.18f;
    [Tooltip("Max swing-hip target rate during ADVANCE, deg/s. Bounds τsw instead of a command cap.")]
    public float walkLiftHipRateDeg = 80f;
    [Tooltip("Share of −τsw sent to the stance hip during the lift (0.6…0.8).")]
    public float walkLiftCoupleHipShare = 0.75f;
    [Tooltip("Share of −τsw sent to the stance ankle, CoP-limited. Not an ankle-angle PD.")]
    public float walkLiftCoupleAnkleShare = 0.15f;
    [Tooltip("Lift-only clamp on the stance-hip feed-forward. Does not change walkSwingLiftCoupleMax.")]
    public float walkLiftCoupleHipMax = 0.95f;
    [Tooltip("|τsw| above this slows ADVANCE rather than saturating the stance chain.")]
    public float walkLiftReactionCap = 0.85f;
    [Tooltip("Contra shoulder amplitude at |τsw|=1 during the lift, deg (minus = forward).")]
    public float walkLiftArmGain = 20f;
    [Tooltip("1 = ADVANCE lumbar target is the measured tilt (D damps, P error 0). Not a fixed lean.")]
    public float walkLiftHoldTrunk = 1f;
    [Tooltip("Pelvis-PD mix on the stance hip during ADVANCE. 1 = full upright, 0 = feed-forward only.")]
    public float walkLiftStancePdScale = 0.25f;

    [System.NonSerialized] public WalkLiftPhase walkLiftPhase;
    [System.NonSerialized] public float lastWalkLiftLoad;
    [System.NonSerialized] public float lastWalkLiftHipTarget;
    [System.NonSerialized] public float lastWalkLiftKneeSwing;
    [System.NonSerialized] public float lastWalkLiftTauSw;
    [System.NonSerialized] public float lastWalkLiftCoupleRamp;
    [System.NonSerialized] public float lastWalkLiftAdvanceScale;
    [System.NonSerialized] public float lastWalkLiftComLead;
    [System.NonSerialized] public float lastWalkLiftComLeadApplied;
    [System.NonSerialized] public float lastWalkLiftLandX;
    [System.NonSerialized] public float lastWalkLiftCaptureX;
    [System.NonSerialized] public float lastWalkLiftCaptureStep;
    [System.NonSerialized] public float lastWalkLiftXiNow;
    [System.NonSerialized] public float lastWalkLiftComVelX;
    [System.NonSerialized] public float lastWalkAnkleFwdDrive;
    [System.NonSerialized] public float lastWalkAnkleFwdRaw;
    [System.NonSerialized] public float lastWalkAnkleDorsiFade;
    [System.NonSerialized] public bool WalkLiftMayTransfer;
    [System.NonSerialized] public bool WalkLiftMaySwap;

    public int WalkLiftPhaseCode => (int)walkLiftPhase;

    private float walkLiftPhaseAge;
    private float walkLiftLoadHoldAge;
    private float walkLiftHipAtUnload;
    private float walkLiftKneeAtUnload;
    private float walkLiftStanceKneeAtUnload;
    private float walkLiftPelvisYAtUnload;
    private float walkLiftStartX;
    private float walkLiftStartY;
    private float walkLiftLandX;
    private float walkLiftLandY;
    private float walkLiftApexY;
    private int walkLiftStanceSign;
    private bool walkLiftLeftGround;
    private bool walkLiftOwns;
    // Dirt Unload/Clear: geo-lift drives the swing (knee shortens, then
    // hip reaches). WalkLift IK / hip-hold is the air track and Advance
    // plant only. Mixing them on a planted sole is the closed chain that
    // kept swingFoot ~1 (hip SPD to unload−5° while geo asked ~50° knee).
    private bool walkLiftAirTrack;
    private bool walkLiftDirtLift;
    private float walkLiftHipTarget;
    private float walkLiftKneeSwing;
    private float walkLiftKneeStance;
    private float walkLiftAnkleSignal;
    private float walkLiftComLead;
    private float walkLiftStanceHipBias;
    private float walkLiftLoadFiltered;
    private float walkLiftHipAtClear;
    private float walkLiftHipPrev;
    private float walkLiftAdvanceClock;
    private float walkLiftPredictedTau;
    private float walkLiftCoupleRamp;
    private float walkLiftAnkleCounter;
    private float walkLiftArmSwing;
    // After abort or a real plant the overlay must not Unload again on
    // the same Stance clock: Idle's load gate is already true (phaseAge
    // ≫ idleSeconds), and a retry into a −15° trunk is the dump.
    private float walkLiftIdleLock;
    // One Unload/Clear attempt per Stance. walklift_plant froze Clear for
    // the whole walk (owns 0.74, Advance 0, swap 0): the clock was gated
    // on WalkLiftPhaseCode != Idle, and Clear never left the slab.
    private bool walkLiftSpentStance;

    private bool WalkLiftOwnsSwing()
    {
        return walkLiftOwns && walkLiftPhase != WalkLiftPhase.Idle;
    }

    // Shared dirt-lift clock. Clear waiting on the probe is already
    // fold=1; Unload uses the longer of geoU and the Unload ramp so the
    // hip does not stay silent while the knee shortens.
    private float WalkLiftDirtBlend(float geoU)
    {
        if (!walkLiftDirtLift)
            return geoU;
        if (walkLiftPhase == WalkLiftPhase.Clear)
            return 1f;
        float unloadU = Smooth01(
            walkLiftPhaseAge / Mathf.Max(0.05f, walkLiftUnloadSeconds));
        return Mathf.Max(geoU, unloadU);
    }

    private void TickWalkLift(
        bool walkSoft,
        bool fallCatchWalk,
        bool splitLegs,
        bool leftIsSwing,
        bool rightIsSwing,
        bool leftGrounded,
        bool rightGrounded,
        float comLiftGate,
        bool walkStancePhase,
        float swingClearance)
    {
        if (useWalkLift == 0
            || !walkSoft
            || fallCatchWalk
            || stepPhaseDriver == null
            || stepPhaseDriver.useFallCatchWalk != 0)
        {
            ResetWalkLift();
            return;
        }

        if (stepPhaseDriver.StandingRestore || stepPhaseDriver.StandingSurrender)
        {
            ResetWalkLift();
            walkLiftIdleLock = 0f;
            return;
        }

        if (!splitLegs || (!leftIsSwing && !rightIsSwing))
        {
            ResetWalkLift();
            return;
        }
        // PhaseCode reports this overlay as 1/2/3. The caller used to pass
        // PhaseCode==0 as walkStancePhase, so Unload Reset on the next step
        // (one frame of LiftKneeFlexDeg, then Idle, walkPhase stuck at 0).
        // WalkStanceClock is the Stance/Transfer bit; once we own, keep it.
        if (!walkStancePhase && !WalkLiftOwnsSwing())
        {
            ResetWalkLift();
            return;
        }

        int sign = leftIsSwing ? 1 : -1;
        if (sign != walkLiftStanceSign)
        {
            bool planted = leftGrounded && rightGrounded;
            // walklift_ankles: sign 0→±1 at walk start is also planted.
            // spent there blocked Idle Unload (owns 0, dump 6.64, swap 0).
            // Keep spent only on a later planted flip, after a Stance
            // already existed.
            bool hadStance = walkLiftStanceSign != 0;
            ResetWalkLift();
            walkLiftStanceSign = sign;
            walkLiftSpentStance = planted && hadStance;
            walkLiftIdleLock = hadStance ? 0.20f : 0f;
        }

        ResolveLegLinkOnce();
        float dt = Time.fixedDeltaTime;
        walkLiftPhaseAge += dt;

        bool swingGrounded = leftIsSwing ? leftGrounded : rightGrounded;
        bool stanceGrounded = leftIsSwing ? rightGrounded : leftGrounded;
        if (!swingGrounded)
            walkLiftLeftGround = true;
        // keep35 was Clear IK into a planted sole. Unload's HoldWalkLiftShorten
        // is the opposite: hip holds, knee shortens, stance knee is the
        // strut. A millimetre of geo-lift air used to Enter Unload, then
        // the probe flicker Reset the overlay before fold=1, Idle saw
        // dirt again and never retried (burn_walk swingFoot 0.9545).
        // Advance may still see the sole return — that is the plant.
        bool dirtHold = walkLiftUnloadFromDirt > 0.5f
            && (walkLiftPhase == WalkLiftPhase.Unload
                || walkLiftPhase == WalkLiftPhase.Clear);
        if (swingGrounded
            && walkLiftPhase != WalkLiftPhase.Idle
            && walkLiftPhase != WalkLiftPhase.Advance
            && !dirtHold)
        {
            ResetWalkLift();
            return;
        }

        float load = EstimateWalkLiftStanceLoad(
            leftIsSwing, leftGrounded, rightGrounded);
        float tau = 1f / (2f * Mathf.PI * 25f);
        float alpha = dt / (tau + dt);
        walkLiftLoadFiltered += (load - walkLiftLoadFiltered) * alpha;
        lastWalkLiftLoad = walkLiftLoadFiltered;

        if (WalkLiftShouldAbort())
        {
            walkLiftSpentStance = true;
            ResetWalkLift();
            walkLiftIdleLock = 0.25f;
            stepPhaseDriver.RequestStandingPlant();
            return;
        }

        switch (walkLiftPhase)
        {
            case WalkLiftPhase.Idle:
                TickWalkLiftIdle(
                    leftIsSwing, comLiftGate, walkStancePhase,
                    swingGrounded, stanceGrounded);
                break;
            case WalkLiftPhase.Unload:
                TickWalkLiftUnload(leftIsSwing, swingGrounded, stanceGrounded);
                break;
            case WalkLiftPhase.Clear:
                TickWalkLiftClear(leftIsSwing, swingGrounded);
                break;
            case WalkLiftPhase.Advance:
                TickWalkLiftAdvance(leftIsSwing, swingGrounded);
                break;
            case WalkLiftPhase.Restore:
                TickWalkLiftRestore(leftIsSwing);
                break;
        }

        if (WalkLiftOwnsSwing())
        {
            walkLiftPredictedTau = PeekWalkLiftSwingHip(leftIsSwing);
            walkLiftCoupleRamp = WalkLiftCoupleRampNow();
        }
        else
        {
            walkLiftPredictedTau = 0f;
            walkLiftCoupleRamp = 0f;
            walkLiftAnkleCounter = 0f;
            walkLiftArmSwing = 0f;
        }
        lastWalkLiftHipTarget = walkLiftHipTarget;
        lastWalkLiftKneeSwing = walkLiftKneeSwing;
        lastWalkLiftTauSw = walkLiftPredictedTau;
        lastWalkLiftCoupleRamp = walkLiftCoupleRamp;
        lastWalkLiftComLead = walkLiftComLead;
        // Plant stays on the Stance split. Dual-support Transfer
        // (standLeg=0) is the dump after a real or scraped lift
        // (walklift_unload t=6.01). The overlay drops itself on plant;
        // this flag must not ask the clock to zero standLeg.
        WalkLiftMayTransfer = false;
        walkLiftAirTrack = WalkLiftOwnsSwing()
            && walkLiftLeftGround
            && !swingGrounded;
        walkLiftDirtLift = WalkLiftOwnsSwing()
            && (walkLiftPhase == WalkLiftPhase.Unload
                || walkLiftPhase == WalkLiftPhase.Clear)
            && !walkLiftAirTrack;
        _ = swingClearance;
    }

    private void TickWalkLiftIdle(
        bool leftIsSwing,
        float comLiftGate,
        bool walkStancePhase,
        bool swingGrounded,
        bool stanceGrounded)
    {
        walkLiftOwns = false;
        walkLiftAnkleSignal = 0f;
        walkLiftComLead = 0f;
        walkLiftStanceHipBias = 0f;
        _ = comLiftGate;
        _ = walkStancePhase;
        if (walkLiftIdleLock > 0f)
        {
            walkLiftIdleLock -= Time.fixedDeltaTime;
            return;
        }
        // PlantStandReady is a closed loop: CoM cannot leave a stacked
        // pair until the swing knee shortens. Dirt Unload is that shorten
        // on the first Stance frames — not Idle 0.10 s, not xi past the
        // toe (walkPhase stuck at 0, single-support ~0.4 %).
        bool dirtReady = walkLiftUnloadFromDirt > 0.5f
            && swingGrounded
            && stanceGrounded;
        if (swingGrounded && !dirtReady)
        {
            walkLiftLoadHoldAge = 0f;
            return;
        }
        walkLiftLoadHoldAge = 0f;
        EnterWalkLiftUnload(leftIsSwing, swingGrounded);
    }

    private void EnterWalkLiftUnload(bool leftIsSwing, bool swingGrounded)
    {
        walkLiftPhase = WalkLiftPhase.Unload;
        walkLiftPhaseAge = 0f;
        walkLiftOwns = true;
        // Dirt start: Clear must not think the sole already left.
        // Air start: keep the latch so Clear does not Reset.
        walkLiftLeftGround = !swingGrounded;
        WalkLiftMayTransfer = false;
        HingeJoint2D swingHip = leftIsSwing ? leftHipJoint : rightHipJoint;
        HingeJoint2D swingKnee = leftIsSwing ? leftKneeJoint : rightKneeJoint;
        HingeJoint2D stanceKnee = leftIsSwing ? rightKneeJoint : leftKneeJoint;
        walkLiftHipAtUnload = swingHip != null ? swingHip.jointAngle : 0f;
        walkLiftKneeAtUnload = swingKnee != null ? swingKnee.jointAngle : kneeBaseAngle;
        walkLiftStanceKneeAtUnload = stanceKnee != null ? stanceKnee.jointAngle : kneeBaseAngle;
        walkLiftPelvisYAtUnload = PelvisWorldY();
        Vector2 ankle = AnkleWorld(leftIsSwing);
        walkLiftStartX = ankle.x;
        walkLiftStartY = ankle.y;
        walkLiftHipTarget = walkLiftHipAtUnload;
        walkLiftHipPrev = walkLiftHipAtUnload;
        walkLiftHipAtClear = walkLiftHipAtUnload;
        walkLiftAdvanceClock = 0f;
        walkLiftKneeSwing = walkLiftKneeAtUnload;
        walkLiftKneeStance = walkLiftStanceKneeAtUnload;
        walkLiftAnkleSignal = 0f;
        walkLiftIdleLock = 0f;
        walkLiftSpentStance = true;
        lastWalkLiftAdvanceScale = 1f;
    }

    private void TickWalkLiftUnload(bool leftIsSwing, bool swingGrounded, bool stanceGrounded)
    {
        float u = Smooth01(walkLiftPhaseAge / Mathf.Max(0.05f, walkLiftUnloadSeconds));
        // Two halves. First the stance strut takes the pelvis (knee to
        // 0, hip extend) while the swing ankle stays silent — Stance
        // already zeroed that CoM loop; dorsiflex here pressed the
        // swing heel (walklift_toe: actAnkleL 0 → 0.4, ankleL −32°
        // while leftGrounded stayed 1). Then the swing knee shortens.
        // Folding on a still-loaded heel is the closed chain.
        float take = Smooth01(Mathf.Clamp01(2f * u));
        float fold = Smooth01(Mathf.Clamp01(2f * u - 1f));
        walkLiftOwns = true;
        HoldWalkLiftShorten(leftIsSwing, fold, take, stanceGrounded);

        if (walkLiftPhaseAge >= walkLiftUnloadSeconds)
        {
            // Dirt Unload stays at fold=1 until the probe actually
            // drops. Entering Clear IK here is keep35 (apex from slab).
            if (swingGrounded && walkLiftUnloadFromDirt > 0.5f)
                return;
            EnterWalkLiftClear(leftIsSwing);
        }
    }

    private void TickWalkLiftClear(bool leftIsSwing, bool swingGrounded)
    {
        // Apex IK while the sole is still down is a closed-chain yank:
        // the hip SPD fights the slab (keep35). Hold the Unload shorten
        // until contact confirms lift-off. Do not Advance from dirt:
        // that lerp returns Y to startY (the slab) even when the foot
        // never left.
        if (swingGrounded || !walkLiftLeftGround)
        {
            HoldWalkLiftShorten(leftIsSwing, 1f, 1f, true);
            return;
        }

        float dur = Mathf.Max(0.05f, walkLiftClearSeconds);
        float u = Smooth01(walkLiftPhaseAge / dur);
        Vector2 hipPoint = HipWorld(leftIsSwing);
        float y = walkLiftStartY + walkLiftClearHeight * MinJerk01(u);
        float x = walkLiftStartX;
        walkLiftApexY = y;
        ApplyWalkLiftIk(
            leftIsSwing, hipPoint, new Vector2(x, y), u,
            walkLiftHipAtUnload - walkLiftHipHoldFlex);
        walkLiftAnkleSignal = walkLiftAnkleDorsi;
        walkLiftStanceHipBias = walkLiftStanceHipExtend * (1f - 0.5f * u);
        walkLiftComLead = WalkLiftDir() * Mathf.Lerp(walkLiftComLeadStart, 0f, u);
        if (walkLiftPhaseAge >= dur)
            EnterWalkLiftAdvance(leftIsSwing);
    }

    // Knee shortens, hip holds the unload angle, stance knee is the
    // strut. Hip P into a planted sole is the torso dump: keep the
    // hold target and let the stance couple take −τsw.
    private void HoldWalkLiftShorten(
        bool leftIsSwing, float fold, float take, bool stanceGrounded)
    {
        walkLiftHipTarget = walkLiftHipAtUnload - walkLiftHipHoldFlex * fold;
        walkLiftHipPrev = walkLiftHipTarget;
        walkLiftKneeSwing = Mathf.Clamp(
            walkLiftKneeAtUnload + walkLiftKneeFlex * fold, 0f, 115f);
        walkLiftKneeStance = Mathf.Lerp(walkLiftStanceKneeAtUnload, 0f, take);
        walkLiftAnkleSignal = 0f;
        walkLiftStanceHipBias = stanceGrounded ? walkLiftStanceHipExtend * take : 0f;
        walkLiftComLead = WalkLiftDir() * walkLiftComLeadStart * fold;
        _ = leftIsSwing;
    }

    private void EnterWalkLiftRestore(bool leftIsSwing)
    {
        walkLiftPhase = WalkLiftPhase.Restore;
        walkLiftPhaseAge = 0f;
        walkLiftOwns = true;
        WalkLiftMaySwap = false;
        WalkLiftMayTransfer = false;
        _ = leftIsSwing;
    }

    private void TickWalkLiftRestore(bool leftIsSwing)
    {
        // Reverse of Unload while the same leg is still swing: fold 1→0
        // so the shortened knee is not the next stance. Overlay keeps
        // the swing ankle silent; CoM stays on the strut.
        float dur = Mathf.Max(0.05f, walkLiftRestoreSeconds);
        float u = Smooth01(walkLiftPhaseAge / dur);
        float fold = 1f - u;
        HoldWalkLiftShorten(leftIsSwing, fold, fold, true);
        if (walkLiftPhaseAge < dur)
            return;
        // Restore used to raise MaySwap on a never-lifted Clear. That
        // is the planted EnterStance dump. Drop the overlay; no swap.
        ResetWalkLift();
        walkLiftSpentStance = true;
        walkLiftIdleLock = 0.20f;
    }

    private void TickWalkLiftAdvance(bool leftIsSwing, bool swingGrounded)
    {
        // Confirmed plant after air: drop the overlay, keep the Stance
        // split. EnterTransfer + standLeg=0 is both-ankle CoM on a −20°
        // trunk (walklift_unload 6.01–6.07).
        // Clock 0.15 is 15 % of Advance: Y has not started down
        // (AdvancePlant opens at 0.40) and X is still near the lift
        // ankle. A millimetre flicker then MaySwap'd that scrape
        // (burn_walk: two swaps in 0.835 s, both soles down, then
        // UpdateState 0.30 / RelaxAllMuscles). Wait until the foot
        // is supposed to be near the slab AND sitting ahead of CoM.
        if (walkLiftLeftGround && swingGrounded)
        {
            if (walkLiftAdvanceClock >= 0.85f
                && WalkLiftCatchPlanted(leftIsSwing))
            {
                walkLiftSpentStance = true;
                ResetWalkLift();
                walkLiftIdleLock = 0.40f;
                WalkLiftMaySwap = true;
                return;
            }
            // Early scrape: IK into a planted sole is keep35. Hold
            // the Unload shorten; abort drop still stands the body.
            HoldWalkLiftShorten(leftIsSwing, 1f, 1f, true);
            return;
        }

        float dt = Time.fixedDeltaTime;
        float dur = Mathf.Max(0.08f, walkLiftAdvanceSeconds);
        StretchWalkLiftLandX(leftIsSwing, dur * (1f - walkLiftAdvanceClock));
        float du = dt / dur;
        float prevClock = walkLiftAdvanceClock;
        float prevHip = walkLiftHipPrev;
        float scale = 1f;

        ApplyWalkLiftAdvancePose(leftIsSwing, prevClock + du);

        float tau = PeekWalkLiftSwingHip(leftIsSwing);
        float cap = Mathf.Max(0.05f, walkLiftReactionCap);
        if (Mathf.Abs(tau) > cap)
        {
            scale = cap / Mathf.Abs(tau);
            walkLiftHipPrev = prevHip;
            walkLiftHipTarget = prevHip;
            ApplyWalkLiftAdvancePose(leftIsSwing, prevClock + du * scale);
            tau = PeekWalkLiftSwingHip(leftIsSwing);
            if (Mathf.Abs(tau) > cap)
            {
                HingeJoint2D swingHip = leftIsSwing ? leftHipJoint : rightHipJoint;
                float hold = swingHip != null ? swingHip.jointAngle : prevHip;
                walkLiftHipTarget = Mathf.MoveTowards(
                    walkLiftHipTarget, hold, walkLiftHipRateDeg * dt);
                walkLiftHipPrev = walkLiftHipTarget;
            }
        }

        walkLiftAdvanceClock = Mathf.Clamp01(prevClock + du * scale);
        lastWalkLiftAdvanceScale = scale;
        walkLiftAnkleSignal = walkLiftLeftGround
            ? walkLiftAnkleDorsi * (1f - AdvancePlant(walkLiftAdvanceClock))
            : 0f;
        walkLiftStanceHipBias = 0f;
        walkLiftComLead = WalkLiftDir()
            * Mathf.Lerp(0f, walkLiftComLeadEnd, MinJerk01(walkLiftAdvanceClock));
        _ = swingGrounded;
    }

    private void ApplyWalkLiftAdvancePose(bool leftIsSwing, float clock)
    {
        clock = Mathf.Clamp01(clock);
        float dur = Mathf.Max(0.08f, walkLiftAdvanceSeconds);
        float rampT = Mathf.Max(0.08f, walkLiftAdvanceHipRamp / dur);
        float hipBlend = MinJerk01(clock / rampT);
        Vector2 hipPoint = HipWorld(leftIsSwing);
        float x = Mathf.Lerp(walkLiftStartX, walkLiftLandX, MinJerk01(clock));
        float plant = AdvancePlant(clock);
        float y = Mathf.Lerp(walkLiftApexY, walkLiftLandY, plant);
        ApplyWalkLiftIk(
            leftIsSwing, hipPoint, new Vector2(x, y), hipBlend, walkLiftHipAtClear);
    }

    private static float AdvancePlant(float clock)
    {
        return Smooth01((clock - 0.40f) / 0.60f);
    }

    private void EnterWalkLiftClear(bool leftIsSwing)
    {
        Vector2 ankle = AnkleWorld(leftIsSwing);
        walkLiftStartX = ankle.x;
        walkLiftStartY = ankle.y;
        walkLiftApexY = walkLiftStartY + walkLiftClearHeight;
        walkLiftPhase = WalkLiftPhase.Clear;
        walkLiftPhaseAge = 0f;
        walkLiftOwns = true;
        walkLiftHipPrev = walkLiftHipTarget;
    }

    private void EnterWalkLiftAdvance(bool leftIsSwing)
    {
        walkLiftLandX = WalkLiftCaptureFootX(leftIsSwing, walkLiftAdvanceSeconds);
        lastWalkLiftLandX = walkLiftLandX;
        walkLiftLandY = walkLiftStartY;
        walkLiftHipAtClear = walkLiftHipTarget;
        walkLiftHipPrev = walkLiftHipTarget;
        walkLiftAdvanceClock = 0f;
        walkLiftPhase = WalkLiftPhase.Advance;
        walkLiftPhaseAge = 0f;
        walkLiftOwns = true;
        lastWalkLiftAdvanceScale = 1f;
    }

    // Plant the swing under live CoM, inside a reachable stride.
    // walklift_air: e^{ωT} lookahead hit walkLiftStepMax 0.55 m,
    // Stretch only grew, Advance never touched (left air 5.50–6.45),
    // supportMargin went negative, swap 0. The ankle cannot put CoP
    // ahead of the toe; the next sole must arrive under CoM, not on
    // an unreachable xi(T).
    private void StretchWalkLiftLandX(bool leftIsSwing, float remainT)
    {
        _ = remainT;
        walkLiftLandX = WalkLiftCaptureFootX(leftIsSwing, 0f);
        lastWalkLiftLandX = walkLiftLandX;
    }

    private float WalkLiftCaptureFootX(bool leftIsSwing, float duration)
    {
        _ = duration;
        float dir = WalkLiftDir();
        float stanceX = StanceAnkleX(leftIsSwing);
        float reach = Mathf.Clamp(walkLiftStepLength, 0.08f, 0.25f);
        lastWalkLiftComVelX = 0f;
        lastWalkLiftXiNow = stanceX;
        lastWalkLiftCaptureX = stanceX + dir * reach;
        lastWalkLiftCaptureStep = reach;
        if (comCalculator == null)
            return stanceX + dir * reach;

        Vector2 com = comCalculator.GetCenterOfMass();
        lastWalkLiftComVelX = WalkComVelocityX;
        lastWalkLiftXiNow = com.x;
        // Small pad past CoM so the plate is a catch, not a trail.
        float catchX = com.x + dir * 0.03f;
        float step = (catchX - stanceX) * dir;
        step = Mathf.Clamp(step, 0.08f, reach);
        lastWalkLiftCaptureX = stanceX + dir * step;
        lastWalkLiftCaptureStep = step;
        return lastWalkLiftCaptureX;
    }

    // Catch plant: the swing sole is a restoring plate, not a trail
    // under the hip. DualLoadMinSpan is the same 2 cm PickStance
    // already trusts as two supports. Quiet / oneg never enter.
    private bool WalkLiftCatchPlanted(bool leftIsSwing)
    {
        if (comCalculator == null || bodyState == null)
            return false;
        Vector2 com = comCalculator.GetCenterOfMass();
        float swingX = leftIsSwing
            ? bodyState.leftFootPosition.x
            : bodyState.rightFootPosition.x;
        float stanceX = leftIsSwing
            ? bodyState.rightFootPosition.x
            : bodyState.leftFootPosition.x;
        float dir = WalkLiftDir();
        if ((swingX - stanceX) * dir < DualLoadMinSpan)
            return false;
        return (swingX - com.x) * dir >= DualLoadMinSpan;
    }

    private void ApplyWalkLiftIk(
        bool leftIsSwing, Vector2 hipPoint, Vector2 ankleTarget, float hipBlend, float hipFrom)
    {
        walkLiftOwns = true;
        if (!SolveCaptureLegIk(hipPoint, ankleTarget, out float thighDeg, out float kneeDeg))
            return;
        float pelvisTilt = vestibularSystem != null ? vestibularSystem.GetPelvisTilt() : 0f;
        float hipIk = pelvisTilt - thighDeg;
        float hipGoal = Mathf.Lerp(hipFrom, hipIk, Mathf.Clamp01(hipBlend));
        float maxDeg = Mathf.Max(0.01f, walkLiftHipRateDeg) * Time.fixedDeltaTime;
        walkLiftHipTarget = Mathf.MoveTowards(walkLiftHipPrev, hipGoal, maxDeg);
        walkLiftHipPrev = walkLiftHipTarget;
        walkLiftKneeSwing = Mathf.Clamp(kneeDeg, 0f, 115f);
        walkLiftKneeStance = kneeBaseAngle;
    }

    private void ApplyWalkLiftKneeTargets(ref float leftKnee, ref float rightKnee)
    {
        if (!WalkLiftOwnsSwing()) return;
        if (walkLiftStanceSign > 0)
        {
            leftKnee = walkLiftKneeSwing;
            rightKnee = walkLiftKneeStance;
        }
        else
        {
            rightKnee = walkLiftKneeSwing;
            leftKnee = walkLiftKneeStance;
        }
    }

    private bool DriveWalkLiftAnkle(
        bool isSwing,
        bool grounded,
        HingeJoint2D joint,
        Muscle flexor,
        Muscle extensor,
        ref float ankleInertia)
    {
        if (!WalkLiftOwnsSwing() || !isSwing) return false;
        if (grounded)
        {
            // Signal 0: do not steal the ankle. Stance already writes 0
            // on a grounded swing; a minus command here is heel pressure
            // and puts the load back on the foot we are trying to free.
            if (Mathf.Abs(walkLiftAnkleSignal) < 0.0001f)
                return false;
            ControlAnkleForBalance(joint, -walkLiftAnkleSignal, flexor, extensor);
            return true;
        }
        if (joint != null && joint.connectedBody != null)
        {
            float flat = Mathf.DeltaAngle(0f, joint.connectedBody.rotation);
            ControlJointToAngleStable(joint, flat, hipPGain, hipDGain,
                errorReferenceDegrees, flexor, extensor, ref ankleInertia);
            return true;
        }
        ControlAnkleForBalance(joint, 0f, flexor, extensor);
        return true;
    }

    private void ApplyWalkLiftStanceHipBias(ref float leftHipCounter, ref float rightHipCounter)
    {
        if (!WalkLiftOwnsSwing() || Mathf.Abs(walkLiftStanceHipBias) < 0.0001f)
            return;
        float cap = WalkLiftOwnsSwing()
            ? Mathf.Max(0.05f, walkLiftCoupleHipMax)
            : Mathf.Max(0.05f, walkSwingLiftCoupleMax);
        if (walkLiftStanceSign > 0)
            rightHipCounter = Mathf.Clamp(rightHipCounter + walkLiftStanceHipBias, -cap, cap);
        else
            leftHipCounter = Mathf.Clamp(leftHipCounter + walkLiftStanceHipBias, -cap, cap);
    }

    // Sol mechanism 2: τstance = τPD * pdScale + ramp(phase) * (−τsw).
    // Same-step predicted swing-hip signal, not last frame's command. The
    // global walkSwingLiftCouple path stays on fall-catch / non-lift walk.
    // Sign is the existing couple: swing flexor (minus) → stance extension
    // (plus) so the planted foot takes the pelvis reaction forward.
    private void ApplyWalkLiftReactionCouple(
        ref float leftHipCounter, ref float rightHipCounter,
        bool leftDrivePelvis, bool rightDrivePelvis,
        bool leftGrounded, bool rightGrounded,
        float pelvisTarget)
    {
        walkLiftAnkleCounter = 0f;
        walkLiftArmSwing = 0f;
        if (!WalkLiftOwnsSwing()) return;

        float ramp = walkLiftCoupleRamp;
        float tau = walkLiftPredictedTau;
        if (Mathf.Abs(tau) < 0.0001f && ramp < 0.0001f)
            return;

        float ff = -tau * ramp;
        float hipShare = Mathf.Clamp01(walkLiftCoupleHipShare);
        float ankleShare = Mathf.Clamp01(walkLiftCoupleAnkleShare);
        float hipMax = Mathf.Max(0.05f, walkLiftCoupleHipMax);
        float hipFf = Mathf.Clamp(ff * hipShare, -hipMax, hipMax);

        float pd = 0f;
        if (vestibularSystem != null)
        {
            float tilt = vestibularSystem.GetPelvisTilt();
            float angVel = vestibularSystem.GetPelvisAngularVelocity();
            float error = Mathf.DeltaAngle(tilt, pelvisTarget)
                / Mathf.Max(1f, pelvisErrorReferenceDegrees);
            float speed = angVel / Mathf.Max(1f, speedReferenceDegPerSec);
            pd = Mathf.Clamp(pelvisPGain * error - pelvisDGain * speed, -1f, 1f);
        }

        float pdScale = 1f;
        if (walkLiftPhase == WalkLiftPhase.Advance)
            pdScale = Mathf.Clamp01(walkLiftStancePdScale);
        else if (walkLiftPhase == WalkLiftPhase.Clear)
            pdScale = Mathf.Lerp(1f, Mathf.Clamp01(walkLiftStancePdScale), ramp);

        // WithHipCounter does Clamp(pd + counter). We want
        // pdScale*pd + hipFf on the stance hip, so the upright loop
        // cannot cancel the feed-forward during ADVANCE.
        float counter = hipFf - pd * (1f - pdScale);

        bool leftIsSwing = walkLiftStanceSign > 0;
        if (leftIsSwing && rightDrivePelvis && rightGrounded)
            rightHipCounter = counter;
        else if (!leftIsSwing && leftDrivePelvis && leftGrounded)
            leftHipCounter = counter;

        float ankleFf = Mathf.Clamp(
            ff * ankleShare, -walkLiftCoupleAnkleShare, walkLiftCoupleAnkleShare);
        ankleFf = LimitWalkLiftAnkleByCop(leftIsSwing, ankleFf);
        walkLiftAnkleCounter = ankleFf;

        float armMag = Mathf.Clamp01(Mathf.Abs(ff));
        float armScale = walkLiftArmGain / Mathf.Max(1f, walkArmShoulderSwing);
        walkLiftArmSwing = armMag * armScale;
    }

    private float LimitWalkLiftAnkleByCop(bool leftIsSwing, float ankleCmd)
    {
        if (Mathf.Abs(ankleCmd) < 0.0001f || bodyState == null || comCalculator == null)
            return ankleCmd;
        Vector2 com = comCalculator.GetCenterOfMass();
        float stanceX = leftIsSwing
            ? bodyState.rightFootPosition.x
            : bodyState.leftFootPosition.x;
        float dir = WalkLiftDir();
        float toe = stanceX + 0.195f * dir;
        float heel = stanceX - 0.065f * dir;
        float toToe = dir >= 0f ? toe - com.x : com.x - toe;
        float toHeel = dir >= 0f ? com.x - heel : heel - com.x;
        if (ankleCmd > 0f && toToe < 0.04f)
            ankleCmd *= Mathf.Clamp01(toToe / 0.04f);
        if (ankleCmd < 0f && toHeel < 0.04f)
            ankleCmd *= Mathf.Clamp01(toHeel / 0.04f);
        return ankleCmd;
    }

    private void ApplyWalkLiftAnkleRemainder(ref float leftAnkleBalance, ref float rightAnkleBalance)
    {
        if (!WalkLiftOwnsSwing() || Mathf.Abs(walkLiftAnkleCounter) < 0.0001f)
            return;
        if (walkLiftStanceSign > 0)
            rightAnkleBalance = Mathf.Clamp(rightAnkleBalance + walkLiftAnkleCounter, -1f, 1f);
        else
            leftAnkleBalance = Mathf.Clamp(leftAnkleBalance + walkLiftAnkleCounter, -1f, 1f);
    }

    private bool TryWalkLiftArmSwing(out float standSign, out float swing)
    {
        standSign = walkLiftStanceSign;
        swing = walkLiftArmSwing;
        return WalkLiftOwnsSwing() && walkLiftArmGain > 0.1f && walkLiftArmSwing > 0.001f;
    }

    private float WalkLiftLumbarTarget(float otherwise)
    {
        if (!WalkLiftOwnsSwing()
            || walkLiftHoldTrunk < 0.5f
            || walkLiftPhase != WalkLiftPhase.Advance
            || vestibularSystem == null)
            return otherwise;
        // P error 0 on the measured tilt holds a dump: walklift_unload
        // Advance locked −15°…−40° then aborted. Recover unless the
        // lean is still a small planned pitch.
        float tilt = vestibularSystem.GetBodyTilt();
        if (Mathf.Abs(tilt) > 8f)
            return otherwise;
        return tilt;
    }

    private float WalkLiftCoupleRampNow()
    {
        switch (walkLiftPhase)
        {
            case WalkLiftPhase.Unload:
                return 0.70f * Smooth01(
                    walkLiftPhaseAge / Mathf.Max(0.05f, walkLiftUnloadSeconds));
            case WalkLiftPhase.Clear:
                return 0.45f + 0.25f * Smooth01(
                    walkLiftPhaseAge / Mathf.Max(0.05f, walkLiftClearSeconds));
            case WalkLiftPhase.Advance:
                return 1f;
            default:
                return 0f;
        }
    }

    private float PeekWalkLiftSwingHip(bool leftIsSwing)
    {
        float hipRef = Mathf.Max(errorReferenceDegrees, walkSwingErrorReferenceDegrees);
        if (leftIsSwing)
        {
            return PredictStableJointSignal(
                leftHipJoint, walkLiftHipTarget, hipPGain, hipDGain, hipRef,
                leftHipFlexor, leftHipExtensor, ref leftHipEffectiveInertia);
        }
        return PredictStableJointSignal(
            rightHipJoint, walkLiftHipTarget, hipPGain, hipDGain, hipRef,
            rightHipFlexor, rightHipExtensor, ref rightHipEffectiveInertia);
    }

    private float WalkLiftComLeadNow()
    {
        // Iteration 2: lead only while the lift owns the swing. A lead on
        // Idle was iteration 3 — dual-support then pulled CoM behind the
        // new sole and dumped at 7 s.
        float lead = WalkLiftOwnsSwing() ? walkLiftComLead : 0f;
        lastWalkLiftComLeadApplied = lead;
        return lead;
    }

    // PlantStandReady (xi past the stance toe). Idle 0.10 s is not
    // that trigger. Split may already be on (weight transfer).
    // walkFirstLiftLead 0 is the former immediate dirt Unload.
    private bool WalkLiftComOnStance(bool leftIsSwing)
    {
        _ = leftIsSwing;
        float need = Mathf.Max(0f, walkFirstLiftLead);
        if (need <= 0.0001f) return true;
        return stepPhaseDriver != null && stepPhaseDriver.PlantStandReady;
    }

    private float EstimateWalkLiftStanceLoad(
        bool leftIsSwing, bool leftGrounded, bool rightGrounded)
    {
        bool stanceGrounded = leftIsSwing ? rightGrounded : leftGrounded;
        bool swingGrounded = leftIsSwing ? leftGrounded : rightGrounded;
        if (!stanceGrounded) return 0f;
        if (!swingGrounded) return 1f;
        if (bodyState == null || comCalculator == null) return 0.5f;
        Vector2 com = comCalculator.GetCenterOfMass();
        float leftX = bodyState.leftFootPosition.x;
        float rightX = bodyState.rightFootPosition.x;
        float stanceX = leftIsSwing ? rightX : leftX;
        float swingX = leftIsSwing ? leftX : rightX;
        if (Mathf.Abs(swingX - stanceX) < DualLoadMinSpan)
        {
            float dSt = Mathf.Abs(com.x - stanceX);
            float dSw = Mathf.Abs(com.x - swingX);
            return dSt <= dSw ? 0.65f : 0.35f;
        }
        float dir = WalkLiftDir();
        float leadX = dir >= 0f ? Mathf.Max(stanceX, swingX) : Mathf.Min(stanceX, swingX);
        float trailX = dir >= 0f ? Mathf.Min(stanceX, swingX) : Mathf.Max(stanceX, swingX);
        float denom = leadX - trailX;
        if (Mathf.Abs(denom) < 0.0001f) return 0.5f;
        float trailShare = Mathf.Clamp01((leadX - com.x) / denom);
        bool stanceIsTrail = dir >= 0f ? stanceX <= swingX : stanceX >= swingX;
        return stanceIsTrail ? trailShare : (1f - trailShare);
    }

    private float DualSupportSpan()
    {
        if (bodyState == null) return 0f;
        return Mathf.Abs(bodyState.rightFootPosition.x - bodyState.leftFootPosition.x);
    }

    private bool WalkLiftShouldAbort()
    {
        if (walkLiftPhase == WalkLiftPhase.Idle) return false;
        if (vestibularSystem != null
            && Mathf.Abs(vestibularSystem.GetBodyTilt()) >= walkLiftAbortTilt)
            return true;
        if (walkLiftPelvisYAtUnload != 0f
            && walkLiftPelvisYAtUnload - PelvisWorldY() >= walkLiftAbortDrop)
            return true;
        return false;
    }

    private void ResetWalkLift()
    {
        walkLiftPhase = WalkLiftPhase.Idle;
        walkLiftPhaseAge = 0f;
        walkLiftLoadHoldAge = 0f;
        walkLiftOwns = false;
        walkLiftLeftGround = false;
        walkLiftAirTrack = false;
        walkLiftDirtLift = false;
        WalkLiftMayTransfer = false;
        WalkLiftMaySwap = false;
        walkLiftAnkleSignal = 0f;
        walkLiftComLead = 0f;
        walkLiftStanceHipBias = 0f;
        walkLiftHipTarget = 0f;
        walkLiftHipPrev = 0f;
        walkLiftHipAtClear = 0f;
        walkLiftAdvanceClock = 0f;
        walkLiftPredictedTau = 0f;
        walkLiftCoupleRamp = 0f;
        walkLiftAnkleCounter = 0f;
        walkLiftArmSwing = 0f;
        lastWalkLiftTauSw = 0f;
        lastWalkLiftCoupleRamp = 0f;
        lastWalkLiftAdvanceScale = 1f;
        lastWalkLiftComLead = 0f;
        lastWalkLiftComLeadApplied = 0f;
        lastWalkLiftLandX = 0f;
        lastWalkLiftCaptureX = 0f;
        lastWalkLiftCaptureStep = 0f;
        lastWalkLiftXiNow = 0f;
        lastWalkLiftComVelX = 0f;
        lastWalkAnkleDorsiFade = 1f;
        walkLiftKneeSwing = kneeBaseAngle;
        walkLiftKneeStance = kneeBaseAngle;
    }

    private float WalkLiftDir()
    {
        if (intent != null && Mathf.Abs(intent.moveX) > 0.01f)
            return Mathf.Sign(intent.moveX);
        return 1f;
    }

    // Minus ankle command = dorsiflex = CoP behind CoM = roll along +X
    // (balance-actuators.mdc). After a capture plant the CoM sits tens of
    // centimetres behind the new plate, P=14 saturates at 7 mm, and the
    // stance ankle full-sends −1 until CoM catches — then overshoots.
    // Cap only that forward-roll sign. Floor is 0.30: walkcap4 at 0.15
    // survived 7.52 s with clip fraction 0.0027 (the gait died before
    // the saturated frames). Plus (brake) is unchanged. Quiet stance,
    // one-leg and fall-catch never enter.
    // First Stance, CoM still on the plate: walkComLeadX is the stance
    // toe plus 6 cm, so the "behind the setpoint" error is ~19 cm. The
    // two clips below were sized for a capture plant (CoM 18–30 cm
    // behind the NEW plate). On initiation they zero the dorsiflex
    // that would roll xi past the toe, so ComLeftSupport never fires
    // (burn_stag 9 mm, swap 0). Skip them until that event. Later
    // stances keep the catch-up protection. No new switch.
    private bool FirstWalkRollOpen()
    {
        if (stepPhaseDriver == null || stepPhaseDriver.SwapCount > 0)
            return false;
        return !stepPhaseDriver.PlantStandReady;
    }

    // First Stance, CoM still on the plate: DrivePelvis on the planted
    // swing is a second strut, and hip/knee K=12 welds it (same weld
    // walkStanceGeoFriction names). Yield the swing hip — quiet muscle,
    // fade the weld — so the stance ankle can roll. Unload / geo-lift
    // start on the first Stance frames; lift is LiftKneeFlexDeg on the
    // swing knee, hip silent on dirt. Not a −swingHipFlex pose (sci_a),
    // not toe-off, not Idle 0.10 s. Quiet / oneg / pf23 never set moveX.
    private bool FirstWalkSwingYield()
    {
        if (stepPhaseDriver == null || !stepPhaseDriver.walkActive)
            return false;
        if (stepPhaseDriver.useFallCatchWalk != 0)
            return false;
        if (intent == null || Mathf.Abs(intent.moveX) < 0.01f)
            return false;
        if (Mathf.Abs(intent.standLeg) < 0.5f)
            return false;
        return FirstWalkRollOpen();
    }

    // 4°+2° pose for the whole first step. FirstWalkRollOpen is only
    // the ankle-clip window (CoM still on the plate). Unload starts on
    // the first Stance frames; holding the pose only until PlantStandReady
    // slammed the lean onto a catch that never came.
    private bool FirstWalkPoseLeanHold()
    {
        return stepPhaseDriver != null && stepPhaseDriver.SwapCount <= 0;
    }

    private float CapWalkAnkleForwardDrive(
        float ankleBalance, float walkMoveDir, bool fallCatchWalk,
        bool walkSoft, bool splitLegs)
    {
        lastWalkAnkleFwdRaw = ankleBalance;
        lastWalkAnkleFwdDrive = ankleBalance;
        if (!walkSoft || fallCatchWalk || !splitLegs)
            return ankleBalance;
        if (FirstWalkRollOpen())
            return ankleBalance;
        if (Mathf.Abs(walkMoveDir) < 0.01f)
            return ankleBalance;
        float maxFwd = Mathf.Clamp01(walkLiftAnkleFwdDriveMax);
        if (maxFwd >= 0.999f)
            return ankleBalance;
        float fwd = -walkMoveDir * ankleBalance;
        if (fwd <= maxFwd)
            return ankleBalance;
        float clipped = -walkMoveDir * maxFwd;
        lastWalkAnkleFwdDrive = clipped;
        return clipped;
    }

    // The ankle CoM loop has no I-term (Muscle.activation is a 50 ms lag).
    // Anti-windup here means: do not keep feeding P=14 a 20–30 cm behind
    // error after a capture plant. That error is already past the 7 mm
    // linear range, so Clamp(P·e) holds −1 for the whole catch-up and
    // integrates forward acceleration. Compress only the dorsiflex
    // (behind) side; plantarflex when CoM is ahead is unchanged.
    // Fade D with the same schedule — otherwise P→0 leaves D·v ≈ +1
    // (full brake) at ordinary walk speed, which is walkcap4 again.
    // Quiet stance, one-leg and fall-catch never enter. ankleComP stays 14.
    private float CompressWalkAnkleDorsiError(
        float ankleOffsetN, float velN, float comRef, float walkMoveDir,
        bool fallCatchWalk, bool walkSoft, bool splitLegs, out float velNOut)
    {
        velNOut = velN;
        lastWalkAnkleDorsiFade = 1f;
        if (!walkSoft || fallCatchWalk || !splitLegs)
            return ankleOffsetN;
        if (FirstWalkRollOpen())
            return ankleOffsetN;
        if (Mathf.Abs(walkMoveDir) < 0.01f)
            return ankleOffsetN;
        float hold = Mathf.Max(0f, walkAnkleDorsiHold);
        float fadeAt = walkAnkleDorsiFade;
        if (fadeAt <= hold + 0.001f)
            return ankleOffsetN;

        float behindN = -walkMoveDir * ankleOffsetN;
        float behindM = behindN * Mathf.Max(0.01f, comRef);
        if (behindM <= hold)
            return ankleOffsetN;

        float fade = 1f - Mathf.Clamp01((behindM - hold) / (fadeAt - hold));
        lastWalkAnkleDorsiFade = fade;
        float linearN = 1f / Mathf.Max(1f, ankleComP);
        float keptBehindN = Mathf.Min(behindN, linearN) * fade;
        velNOut = velN * fade;
        return -walkMoveDir * keptBehindN;
    }

    private Vector2 HipWorld(bool leftIsSwing)
    {
        HingeJoint2D hip = leftIsSwing ? leftHipJoint : rightHipJoint;
        if (hip == null) return Vector2.zero;
        return hip.transform.TransformPoint(hip.anchor);
    }

    private Vector2 AnkleWorld(bool leftIsSwing)
    {
        HingeJoint2D ankle = leftIsSwing ? leftAnkleJoint : rightAnkleJoint;
        if (ankle == null) return Vector2.zero;
        return ankle.transform.TransformPoint(ankle.anchor);
    }

    private float StanceAnkleX(bool leftIsSwing)
    {
        HingeJoint2D ankle = leftIsSwing ? rightAnkleJoint : leftAnkleJoint;
        if (ankle == null) return 0f;
        return ankle.transform.TransformPoint(ankle.anchor).x;
    }

    private float PelvisWorldY()
    {
        if (leftHipJoint != null && leftHipJoint.connectedBody != null)
            return leftHipJoint.connectedBody.position.y;
        if (rightHipJoint != null && rightHipJoint.connectedBody != null)
            return rightHipJoint.connectedBody.position.y;
        return 0f;
    }

    private static float Smooth01(float t)
    {
        t = Mathf.Clamp01(t);
        return t * t * (3f - 2f * t);
    }

    private static float MinJerk01(float t)
    {
        t = Mathf.Clamp01(t);
        float t2 = t * t;
        float t3 = t2 * t;
        float t4 = t3 * t;
        float t5 = t4 * t;
        return 10f * t3 - 15f * t4 + 6f * t5;
    }
}
