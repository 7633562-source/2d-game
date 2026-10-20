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

}
