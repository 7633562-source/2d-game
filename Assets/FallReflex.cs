using System.Globalization;
using UnityEngine;

// ─── THE FALL REFLEX (29.09, task "Tilt-Platform Stand"; owner's addendum 29.09 02:05) ───
//
// Owner (verbatim, RU): "Когда человек падает, по умолчанию должна быть защитная реакция,
// чтобы не упасть. Например, выставить вперед руки или выставить вперед одну из ног... он
// падает все, как тряпичная кукла, в то время как на него должны срабатывать алгоритмы
// внутренней устойчивости... он должен выставлять ногу, выставлять руки, отгибать немного
// назад голову, чтобы ей не удариться — в общем, вести себя как человек."
//
// WHAT. Below the balance core's recovery set a fall is already unavoidable; until now the
// rig's answer there was the give-layer alone (BalanceController: Falling -> RelaxAllMuscles,
// every muscle zeroed) — the measured "ragdoll" the owner rejected (record take
// tiltA_ref_026_h48: margin crosses 0 at 291.705 s, the shoulder stays inside ~3 deg and the
// elbow inside ~18 deg until 292.6, the flail after that is freefall). This component is the
// protective layer that wraps the give: the rest of the body keeps going limp, while three
// channels run a short, deterministic reflex:
//   ARMS  - both shoulders thrust forward-down, elbows part-way open (the brace),
//           emitted through ArmDrive's command,
//   STEP  - ONE leg steps forward (hip flexion + knee lift; the unloaded foot goes first),
//   HEAD  - the neck/head keep the rig's own world-upright PD (the head does not flop
//           forward with the body) with a small backward bias (default -8 deg).
//
// FIRE. The earliest clean "inevitable" signal of the record take: BodyStateEstimator
// supportMargin < fireMargin (default 0) held for confirmFrames physics frames
// (24 ~ 0.12 s at 200 Hz) with at least ONE FOOT GROUNDED — 0.5 s before the automaton
// names Falling. The grounded-foot requirement + the confirm are what keep a landing out
// (the drop record's airborne arrival crosses -0.06 with both feet up, and its
// post-landing wobble dips to -0.10 for 0.035 s — priced in round 3; the tilt takes'
// pre-fall runs are 48-50 frames). Only while the body is still a STANDING body
// (locoState Standing and |tilt| < 60 deg): a walk's normal single support crosses
// margin 0 by design and must never trip this, and a body already down is the get-up's
// business, not the reflex's. Latched until the body is standing cleanly again (release),
// a hold cap (default 4 s), or the fall is over; a later fall in the same take fires again.
//
// OWNERSHIP (one writer per joint). ARMS: the brace is written into ArmDrive's own command
// (reflexShoulderDeg/reflexElbowDeg) and ArmDrive keeps sole ownership of the arm muscles —
// a direct muscle write loses the per-frame tug of war with ArmDrive's re-drive (probe
// take tiltA_r3_probe: actShoulderL pinned at 0.100, shoulderAngleL stalled at -8 deg). The
// arms are handed back the moment the body leaves Standing so the rig's own press (hands
// toward the ground) runs unchanged. STEP: the walking layers are at rest in a stand, so
// the stepping leg's hip/knee are driven here (with a fast slew, the balance core pulling
// the other way each frame); the leg is returned to the rig once the swing foot lands
// (+0.4 s). NECK/HEAD: the rig's own world-upright PD is replicated here (the give layer
// would let the head flop with the ragdoll). Released, it writes nothing and every owner
// resumes bit for bit. It is INERT without a fire, so every take that never falls
// (anchors, stands, fences) keeps its bytes; `-fallReflex 0` disarms it entirely.
//
// KNOBS (all read once in Awake):
//   -fallReflex 0/1        arm it (default 1 in the trial and the played scene)
//   -fallReflexMargin m    fire when supportMargin < m (default 0)
//   -fallReflexFrames n    consecutive frames above to confirm (default 24 = 0.12 s)
//   -fallReflexDelay s     spinal latency before the arms move (default 0.04)
//   -fallReflexArmRamp s   arms ramp-in time (default 0.12)
//   -fallReflexArmDeg d    shoulder target, minus = forward-down (default -65)
//   -fallReflexElbowDeg d  elbow target, minus = folded; -28 = part open (default -28)
//   -fallReflexStepDelay s before the step (default 0.10)
//   -fallReflexStepRamp s  step ramp-in (default 0.25)
//   -fallReflexStepSlew s  step muscle slew, per second (default 60)
//   -fallReflexHipDeg d    stepping hip target, minus = forward (default -35)
//   -fallReflexKneeDeg d   stepping knee target, plus = lift (default 25)
//   -fallReflexHeadBias d  extra head target bias, deg, minus = back (default -8)
//   -fallReflexHold s      release after this many seconds (default 4; 0 = never by time)
//   -fallReflexLog 0/1     one line on fire/release/fire-trace (default 1)
//   -fallReflexForce s     PROBE ONLY: fire at t=s regardless of the margin (default off)
//   -fallReflexUnwind 0/1  the clean-stand release becomes a wind-back ramp (default 1;
//                          0 = the r4..r8 instant release, kept as the fence control)
//   -fallReflexUnwindRamp s  the wind-back ramp, s (default 1.0)
//   -fallReflexUnstep 0/1  the unwind RELOCATES the stepped-out foot (a real step back to
//                          its home x) instead of rotating a planted leg (default 1; 0 = the
//                          r9 planted-joint wind-back, kept as the fence control)
//   -fallReflexUnstepWait s  cap for the whole un-step: wait for the free window + drive
//                            (default 3.0)
//   -fallReflexUnstepHome m  a foot within this of its target x counts as placed (default 0.03)
//   -fallReflexUnstepGain d  hip-target degrees per metre of foot-x error (default 110)
//   -fallReflexUnstepFold d  knee fold kept while the foot travels (default 18)
//   -fallReflexUnstepGap m   the base keeps this much room behind a BACKWARD-DRIFTING CoM:
//                            the rearmost foot is re-placed to (CoM - gap), a real rear catch
//                            (default 0.25)
//   -fallReflexUnstepHand 0/1  THE PLANT HAND-OVER (round 11): keep the un-step's drive across a
//                            locoState flip (a Walking / Falling transit) so the foot completes
//                            its plant; arms go back to the rig at once. BUILT, PRICED, REFUTED on
//                            the fine probe: both doses fell (gap .25: 26.855 s; gap .32: 25.98 s)
//                            where the abort route survives 40 s. DEFAULT 0 = the r10 abort route;
//                            1 keeps the device for future variants (support-first / fast-place).
//   -fallReflexUnstepHandFloor m  engage the hand-over only above this margin (default -0.35)
//   -fallReflexUnwindGate s   the clean-stand window before a release/unwind (default 1.0)
//   -fallReflexXi 0/1      THE XI STEP RUNG (round 20, the balance-refs ladder): ALSO fire when
//                          the capture point xi = com + v*sqrt(h/g) leaves the support hull of
//                          the grounded colliders - whichever arm trips first. Default 0 = off;
//                          the margin arm is then alone and the take is byte for byte the old one.
//   -fallReflexXiFrames n  consecutive frames outside the hull to confirm (default 24)
//   -fallReflexXiLead s    the MOVING-HULL term: compare xi to the edge predicted this far ahead
//                          from the edge's own velocity (default 0 = the current edge)
//   -fallReflexXiEps m     the hull epsilon band (default 0.005, the ladder's own)
//
// ROUND 22 (the r21 consult [[CONSULT-0930-0459]] + the owner-facing r21 addendum):
//   -fallReflexFallingGate 0/1   the fire test also runs from Falling while the margin sits below
//                                -fallReflexFallingGateMargin (default 0) - a release that lands
//                                mid-topple can re-fire (the r21 wall: "fire gate Standing-only").
//   -fallReflexRearm 0/1     THE RE-STATION STEP: while latched, once a recovery window (margin >=
//                            -fallReflexRearmFreeLevel, default 0.05, held -fallReflexRearmFreeHold,
//                            default 0.15 s) is re-earned, a margin / capture-point re-breach
//                            (the r20 xi arm reused as the trip) confirmed for -fallReflexRearmFrames
//                            (default 12) starts a fresh foot-placement episode (the r10 un-step
//                            engine). Cooldown -fallReflexRearmCooldown (0.25) since the last episode
//                            end / step landing; the reflex's own step keeps the leg for
//                            -fallReflexRearmStepHold (0.45) before a re-arm may take it;
//                            -fallReflexRearmMax (3) episodes per latch. The placement law gains
//                            THE FRONT CATCH: when the CoM owns a base ahead of the homes the
//                            FOREMOST foot is re-placed to (comX + gap) - the re-station the r21
//                            addendum names (a rear catch overturned by the deck's reversal).
//   -fallReflexArmHold 0/1   defer the arms->press hand-over while the catch-step is in flight
//                            (capped by -fallReflexArmHoldMax, default 1.2 s) - the consult's
//                            "keep the arm brace alive through the first Falling pass". 
//
// ROUND 25 (the r24 +40-wall analysis): THE DIVE TRIP + THE CAP GUARD.
//   -fallReflexDive 0/1        ALSO start a re-station catch on the CoM's run-away ("the dive"):
//                              |comVelocity.x| >= -fallReflexDiveVel confirmed for
//                              -fallReflexDiveFrames. The record's cruise is 0.36 m/s; the dive
//                              crossed 0.5 at 30.03 and 0.7 at 30.155 - where the xi arm fired.
//   -fallReflexDiveVel m/s     the dive speed gate (default 0.55).
//   -fallReflexDiveFrames n    confirm frames (default 6 = 0.03 s at 200 Hz).
//   -fallReflexCapGuard 0/1    the latch cap releases only with no catch in flight and margin
//                              >= -fallReflexCapMargin, or the body down. Default 0 = the old cap.
//   -fallReflexCapMargin m     the guard's margin floor (default 0.05).
//
// THE UNWIND (round 9, 29.09). The release used to be a snap: the arms jumped out of the
// brace and the caught leg stayed stepped - probe tiltA_r4_probe left a permanent split
// stance (release 23.665 s, a fresh breach 25.87 s, the fall 26.22 s on a near-level deck).
// Now the release moment itself (Standing + both feet grounded + margin > fireMargin for
// 1.0 s) STARTS a wind-back: over unwindRamp the arm command, the stepped leg (a real step
// back through the same joint drive that stepped it out) and the head bias all ramp to
// neutral, and only then does every owner resume. A body that leaves Standing mid-wind-back
// aborts it on the spot (the fall's own press path runs unchanged), the hold cap and the
// never-recovered paths are untouched, so a take that never recovers keeps its bytes.
[DefaultExecutionOrder(20)]
public class FallReflex : MonoBehaviour
{
    private struct Drive
    {
        public HingeJoint2D joint;
        public Muscle flexor;
        public Muscle extensor;
        public float inertia;
    }

    // ─── knobs ───
    private bool armed = true;
    private float fireMargin;
    private int confirmFrames = 24;       // 0.12 s at 200 Hz - rides out a landing wobble
    // CYCLE 120 - THE WALK-ON GUARD (the settle release's deferral; armed via
    // balance.settleReleaseGuardT). While the guard lives the catch-step fire waits
    // for the body to read QUIET: both feet grounded, |comVelX| at or under the bar,
    // the margin recovered, held for the quiet span. The window's own cap (set at
    // arming) ends the freeze, so a real fall keeps its protection. CYCLE 121:
    // landed default = the c120 class winner (the quiet span 1.0 s;
    // -poseWalkSettleGuard 0 restores the pre-121 law).
    private float settleGuardMps = 0.15f;     // -fallReflexSettleGuardMps
    private float settleGuardQuiet = 1.0f;    // -fallReflexSettleGuardQuiet
    private float settleGuardMargin;          // -fallReflexSettleGuardMargin
    private float settleGuardT;               // the quiet span's own clock, s
    private float settleGuardAge;             // time spent in the guard, s (diagnostics)
    private bool settleGuardSeen;             // the guard episode's own latch
    private float spinalDelay = 0.04f;
    private float armRamp = 0.12f;
    private float shoulderDeg = -65f;
    private float elbowDeg = -28f;
    private float stepDelay = 0.10f;
    private float stepRamp = 0.25f;
    private float hipDeg = -35f;
    private float kneeDeg = 25f;
    private float headBiasDeg = -8f;      // the head tips slightly back (the owner's ask)
    private float holdSeconds = 4f;       // the latch cap: covers the fall; the get-up resumes after
    private float stepSlewPerSecond = 60f; // the step leg outruns the balance core's per-frame pull
    private bool logTrace = true;
    private float forceTime = -1f;
    private bool unwindEnabled = true;     // round 9: the clean-stand release becomes a wind-back
    private float unwindRamp = 1.0f;
    private bool unstepEnabled = true;     // round 10: the unwind relocates the foot (a real step)
    private float unstepWait = 3.0f;
    private float unstepHomeM = 0.03f;
    private float unstepGainDeg = 110f;
    private float unstepFoldDeg = 18f;
    private float unstepGapM = 0.25f;      // room the base keeps behind a drifting-backward CoM
    private bool unstepHand = false;       // round 11: the plant hand-over - BUILT, PRICED, REFUTED:
                                           // keeping the drive across the locoState flip made BOTH
                                           // doses fall (26.855 / 25.98) where the r10 abort route
                                           // (this default) survives 40 s: the rig's own mid-flight
                                           // plant beats the servo extended through a catch (the
                                           // foot chased a target while the CoM outran the support).
                                           // Kept behind -fallReflexUnstepHand 1 for future variants.
    private float unstepHandFloorM = -0.35f; // engage only above this margin
    private float unwindGateSeconds = 1.0f; // round 11 knob: the clean-stand window before a release

    private BalanceController balance;
    private BodyStateEstimator state;
    private VestibularSystem vest;
    private ArmDrive arms;
    private PoseOrientation orient;       // round 17: the self-orientation core (may be absent)
    private bool orientTried;             // resolved once, lazily (the attach sites add it late)
    private bool orientRest;              // the current latch drives the arms along the body
    private float headBiasNow;            // the live head bias: the pose branch may tuck it
    public int OrientRestCount { get; private set; }

    private Drive neck, head;
    private Drive stepHip, stepKnee;
    private int stepSide = 1;             // 0 = left steps, 1 = right (default)

    private bool latched;
    private bool forceDone;
    private bool armsOwned;               // the brace rides ArmDrive's command (one writer per joint)
    private bool stepOwned;               // the step leg is driven here until it lands
    private bool stepLeft;                // the swing foot left the deck
    private float stepLandedAt = -1f;
    private float latency;                // seconds since fire
    private float fireTime = -1f;
    private int marginRun;
    private float releaseAge;
    private bool unwinding;               // the clean-stand release became a wind-back (round 9)
    private float unwindStart = -1f;

    // ─── round 10: the un-step (see THE UN-STEP below) ───
    private const float unstepFoldS = 0.5f;    // lift phase cap before driving anyway
    private const float unstepSettleS = 0.15f; // hold home briefly so the foot plants
    private float xL0, xR0, hip0L, hip0R, knee0L, knee0R;   // the base stance, captured at Start
    private Drive unstepHip, unstepKnee;
    private Collider2D[] unstepSoles = new Collider2D[4];
    private readonly ContactPoint2D[] unstepContacts = new ContactPoint2D[8];
    private ContactFilter2D unstepFilter;
    private bool unstepFilterReady;
    private bool unstepActivated;
    private bool unstepDone;
    private int unstepSide = -1;          // 0 = left, 1 = right
    private float unstepStart = -1f;
    private float unstepHomeX;
    private float unstepTargetX;          // the live target (home, or the CoM's own gap)
    private bool unstepArrived;
    private float unstepHipTargetDeg;
    private float unstepKneeTargetDeg;
    private float unstepHipHoldDeg;
    private float unstepKneeHoldDeg;
    private float unstepHipHomeDeg;
    private float unstepKneeHomeDeg;
    private float unstepSettle;
    private float unstepLogClock;
    private bool unstepFreeSeen;
    private bool unstepHandover;           // round 11: the plant hand-over engaged in this unwind

    // ─── round 20: THE XI STEP RUNG (the refs ladder: ankle -> step -> brace) ───
    private bool xiArm;                    // -fallReflexXi (default 0)
    private int xiFrames = 24;             // -fallReflexXiFrames
    private float xiLead;                  // -fallReflexXiLead s (the moving-hull prediction)
    private float xiEps = 0.005f;          // -fallReflexXiEps m
    private const float xiEdgeTau = 0.12f; // edge-velocity EMA time constant, s
    private int xiRun;
    private HingeJoint2D xiAnkleL, xiAnkleR;
    private bool xiEdgeInit;
    private float xiEdgePrevMin, xiEdgePrevMax;
    private float xiEdgeVelMin, xiEdgeVelMax;
    private float xiLast;                  // the xi the arm last tested (trace)
    private float xiHullMinLast, xiHullMaxLast;
    private string fireArm = "-";          // which arm fired: margin / xi / forced

    // ─── round 22: THE RE-ENGAGE PACKAGE (the r21 consult [[CONSULT-0930-0459]]) ───
    private bool fallingGate;              // -fallReflexFallingGate (default 0): the fire test also
                                           // runs from Falling while the margin sits below the floor
    private float fallingGateMargin;       // -fallReflexFallingGateMargin (default 0)
    private bool rearmEnabled;             // -fallReflexRearm (default 0): THE RE-STATION STEP. The
                                           // r21 wall: the reflex fires once per latch, the deck
                                           // reverses and the second breach runs unanswered (fell
                                           // 29.055 / the release->refire route fell 29.195). This
                                           // starts a FRESH foot-placement episode (the r10 un-step
                                           // engine - never drags a loaded foot) while the latch
                                           // holds, on a margin / capture-point re-breach, once a
                                           // recovery window has been re-earned.
    private float rearmMargin;             // -fallReflexRearmMargin (default 0)
    private int rearmFrames = 12;          // -fallReflexRearmFrames (0.06 s at 200 Hz)
    private float rearmCooldown = 0.25f;   // -fallReflexRearmCooldown s between episodes
    private float rearmStepHold = 0.45f;   // -fallReflexRearmStepHold s: how long the reflex's own
                                           // step keeps the leg before a re-arm may take it
    private float rearmFreeLevel = 0.05f;  // -fallReflexRearmFreeLevel m: the recovery margin
    private float rearmFreeHold = 0.15f;   // -fallReflexRearmFreeHold s: how long it must hold
    private float rearmHold = 0.20f;       // -fallReflexRearmHold s: post-arrival hold before the
                                           // episode releases (r22d: the chain's cooldown head)
    private float rearmDriftEps = 0.15f;   // -fallReflexRearmDriftEps m/s: only a drift below this
                                           // (backward) picks the REAR side; else FRONT (r22e)
    private int rearmMax = 3;              // -fallReflexRearmMax: episodes per latch
    private float rearmLeadT;              // -fallReflexRearmLead (default 0): ROUND 23 - THE PRED-LAND
                                           // TERM. The r22 price: the last catch fired 29.53, arrived
                                           // 29.74 at x 0.488 while the com (0.499, running 0.94 ->
                                           // 1.55 m/s) had eaten the whole com+gap standoff - the foot
                                           // landed UNDER the moving body and never loaded. With
                                           // leadT > 0 the re-station target aims (leadT x the live
                                           // com drift) beyond the instantaneous com+gap: the foot
                                           // lands ahead of the capture point at touchdown.
                                           // 0 = the r22 law byte for byte.
    private bool armHold;                  // -fallReflexArmHold (default 0): keep the arm brace
                                           // through a Falling pass while the catch-step is in
                                           // flight (hand to press no later than armHoldMax)
    private float armHoldMax = 1.2f;       // -fallReflexArmHoldMax s
    private bool rearmEpisode;             // an episode is running (the re-arm route)
    private float rearmArriveT = -1f;      // r22c: arrival time of the running episode
    private bool rearmFreed;               // the recovery window has been re-earned
    private float rearmFreeAge;            // the live run of margin >= rearmFreeLevel
    private float rearmLastEnd = -1f;      // last episode end (cooldown base)
    private int rearmRun;                  // the re-arm trigger's confirm run
    private int rearmCount;                // episodes started this latch
    public int RearmCount { get { return rearmCount; } }

    // ROUND 25: THE DIVE TRIP + THE CAP GUARD (the r24 +40-wall analysis).
    private bool diveArm;                  // -fallReflexDive (default 0): a CoM run-away starts a catch
    private float diveVel = 0.55f;         // -fallReflexDiveVel m/s: |comVelocity.x| at/above this...
    private int diveFrames = 6;            // -fallReflexDiveFrames: ...confirmed this long trips
    private int diveRun;                   // the dive trip's live confirm run
    private int diveCount;                 // catch episodes started on the dive trip
    public int DiveCount { get { return diveCount; } }
    private bool capGuard;                 // -fallReflexCapGuard (default 0): the 4 s latch cap
    private float capMargin = 0.05f;       // -fallReflexCapMargin m: releases only with no catch in
    private int capDefer;                  // flight and margin >= this (or the body down); deferrals
    private float capFirstDeferT = -1f;    // are counted - the guard can never hang the latch
    public int CapDeferCount { get { return capDefer; } }
    public float CapFirstDeferTime { get { return capFirstDeferT; } }
    public float XiFireTime { get; private set; }   // the xi arm's own fire time, -1 until one

    // ROUND 26: THE ARRIVED PRESS (the pre-dive brake) + THE ARRIVAL GRACE.
    private bool brakeArm;                 // -fallReflexBrake (default 0): after a catch ARRIVES,
                                           // while the CoM still coasts (|comVelX| >= brakeVel) the
                                           // arrived hold presses the support up-slope under the
                                           // body - the foot-x servo aimed brakeM ahead of the
                                           // landed foot (the planted foot cannot follow; the
                                           // pelvis takes the reaction). r25's option (b).
    private float brakeVel = 0.30f;        // -fallReflexBrakeVel m/s: |comVelX| at/above this engages
    private float brakeM = 0.06f;          // -fallReflexBrakeM m: the press distance (up-slope)
    private float brakeHold;               // -fallReflexBrakeHold s: extend the arrived hold while
                                           // pressing (0 = the 0.15 s settle only)
    private float brakeDir = 1f;           // -fallReflexBrakeDir +1/-1: the probe's sign flip
    private int brakeSide;                 // -fallReflexBrakeSide 0/1: 0 = the just-arrived leg,
                                           // 1 = the OTHER (loaded) leg
    private bool brakeEngaged;             // an engagement is live this episode (log/counter)
    private bool brakeOtherReady;          // side B: the other leg's snapshot is taken
    private Drive brakeOtherHip;           // side B: the other leg's thigh drive
    private float brakeOtherHipDeg;        // side B: its angle at engage
    private int brakeCount;                // engagements (episodes pressed)
    public int BrakeCount { get { return brakeCount; } }
    private float rearmGrace;              // -fallReflexRearmGrace (default 0 = off): a new episode
                                           // also needs this long since the last ARRIVAL (the r25
                                           // d45f6 price: a fresh episode chained 0.02 s after an
                                           // arrival and grabbed the just-planted support).
    private float rearmLastArriveT = -1f;  // the last arrival time (the grace base)

    // ROUND 27: THE ANKLE-RAIL RE-SEAT (the balance-refs item 3(b): "replant when the planted
    // foot has... turned too far"; the r24 wall analysis named the binder: the right ankle railed
    // at its +45 stop ~0.45 s before the criterion - "the CoP has no more travel" - while the -35
    // tail dive (com 0.37 -> 1.19 m/s) ran and the chain's own fires (margin/xi/dive) chased it).
    // A foot whose ankle sits within railBand of its own stop is a foot whose CoP travel is
    // spent: a re-seat restores the flat follow pose of that foot and with it its push range.
    // The rail trip starts a fresh placement the way the other trips do, gated by the margin so
    // it can only fire when the support is tight. All knobs default off = byte for byte.
    private bool railArm;                  // -fallReflexRearmRail 0/1 (default 0): the trip arm
    private float railBand = 3f;           // -fallReflexRearmRailBand deg: "railed" = within this
                                           // many degrees of either ankle stop
    private float railMargin = 0.10f;      // -fallReflexRearmRailMargin m: the support-margin gate
    private int railCount;                 // fires whose rail condition held
    private float railFirstT = -1f;        // the first such fire, -1 until one
    public int RailCount { get { return railCount; } }
    public float RailFirstTime { get { return railFirstT; } }

    public bool Fired { get { return latched; } }
    // Read-only: true while this reflex owns the arm command (the brace reflex yields the
    // arms to it while this is set, so the two never write one joint in one frame).
    public bool ArmsOwned { get { return armsOwned; } }
    public float FireTime { get { return fireTime; } }
    public int StepSide { get { return stepSide; } }
    public float UnwindStartTime { get; private set; }   // first unwind start, -1 until one
    public float UnwindDoneTime { get; private set; }    // finish time of the first unwind
    public int UnwindCount { get; private set; }
    public float UnstepStartTime { get; private set; }   // round 10: the un-step, -1 until one
    public float UnstepDoneTime { get; private set; }
    public float UnstepHandoverTime { get; private set; } // round 11: the hand-over flip, -1 until one

    void Awake()
    {
        armed = GetFloat("-fallReflex", armed ? 1f : 0f) > 0.5f;
        fireMargin = GetFloat("-fallReflexMargin", fireMargin);
        confirmFrames = Mathf.Clamp(Mathf.RoundToInt(GetFloat("-fallReflexFrames", confirmFrames)), 1, 60);
        settleGuardMps = Mathf.Max(0f, GetFloat("-fallReflexSettleGuardMps", settleGuardMps));
        settleGuardQuiet = Mathf.Max(0f, GetFloat("-fallReflexSettleGuardQuiet", settleGuardQuiet));
        settleGuardMargin = GetFloat("-fallReflexSettleGuardMargin", settleGuardMargin);
        spinalDelay = Mathf.Clamp(GetFloat("-fallReflexDelay", spinalDelay), 0f, 0.5f);
        armRamp = Mathf.Max(0.02f, GetFloat("-fallReflexArmRamp", armRamp));
        shoulderDeg = Mathf.Clamp(GetFloat("-fallReflexArmDeg", shoulderDeg), -85f, 85f);
        elbowDeg = Mathf.Clamp(GetFloat("-fallReflexElbowDeg", elbowDeg), -135f, -5f);
        stepDelay = Mathf.Clamp(GetFloat("-fallReflexStepDelay", stepDelay), 0f, 1f);
        stepRamp = Mathf.Max(0.05f, GetFloat("-fallReflexStepRamp", stepRamp));
        hipDeg = Mathf.Clamp(GetFloat("-fallReflexHipDeg", hipDeg), -90f, 45f);
        kneeDeg = Mathf.Clamp(GetFloat("-fallReflexKneeDeg", kneeDeg), -5f, 90f);
        headBiasDeg = Mathf.Clamp(GetFloat("-fallReflexHeadBias", headBiasDeg), -30f, 30f);
        holdSeconds = Mathf.Max(0f, GetFloat("-fallReflexHold", holdSeconds));
        logTrace = GetFloat("-fallReflexLog", logTrace ? 1f : 0f) > 0.5f;
        if (HasArg("-fallReflexForce")) forceTime = Mathf.Max(0f, GetFloat("-fallReflexForce", -1f));
        unwindEnabled = GetFloat("-fallReflexUnwind", unwindEnabled ? 1f : 0f) > 0.5f;
        unwindRamp = Mathf.Max(0.1f, GetFloat("-fallReflexUnwindRamp", unwindRamp));
        unstepEnabled = GetFloat("-fallReflexUnstep", unstepEnabled ? 1f : 0f) > 0.5f;
        unstepWait = Mathf.Clamp(GetFloat("-fallReflexUnstepWait", unstepWait), 0.2f, 10f);
        unstepHomeM = Mathf.Clamp(GetFloat("-fallReflexUnstepHome", unstepHomeM), 0.005f, 0.20f);
        unstepGainDeg = Mathf.Clamp(GetFloat("-fallReflexUnstepGain", unstepGainDeg), 10f, 400f);
        unstepFoldDeg = Mathf.Clamp(GetFloat("-fallReflexUnstepFold", unstepFoldDeg), 0f, 60f);
        unstepGapM = Mathf.Clamp(GetFloat("-fallReflexUnstepGap", unstepGapM), 0.10f, 0.60f);
        unstepHand = GetFloat("-fallReflexUnstepHand", unstepHand ? 1f : 0f) > 0.5f;
        unstepHandFloorM = Mathf.Clamp(GetFloat("-fallReflexUnstepHandFloor", unstepHandFloorM), -1f, 0f);
        unwindGateSeconds = Mathf.Clamp(GetFloat("-fallReflexUnwindGate", unwindGateSeconds), 0.10f, 3f);
        xiArm = GetFloat("-fallReflexXi", xiArm ? 1f : 0f) > 0.5f;
        xiFrames = Mathf.Clamp(Mathf.RoundToInt(GetFloat("-fallReflexXiFrames", xiFrames)), 1, 120);
        xiLead = Mathf.Clamp(GetFloat("-fallReflexXiLead", xiLead), 0f, 2f);
        xiEps = Mathf.Clamp(GetFloat("-fallReflexXiEps", xiEps), 0f, 0.05f);
        fallingGate = GetFloat("-fallReflexFallingGate", fallingGate ? 1f : 0f) > 0.5f;
        fallingGateMargin = GetFloat("-fallReflexFallingGateMargin", fallingGateMargin);
        rearmEnabled = GetFloat("-fallReflexRearm", rearmEnabled ? 1f : 0f) > 0.5f;
        rearmMargin = GetFloat("-fallReflexRearmMargin", rearmMargin);
        rearmFrames = Mathf.Clamp(Mathf.RoundToInt(GetFloat("-fallReflexRearmFrames", rearmFrames)), 1, 120);
        rearmCooldown = Mathf.Clamp(GetFloat("-fallReflexRearmCooldown", rearmCooldown), 0f, 3f);
        rearmStepHold = Mathf.Clamp(GetFloat("-fallReflexRearmStepHold", rearmStepHold), 0.05f, 3f);
        rearmFreeLevel = Mathf.Clamp(GetFloat("-fallReflexRearmFreeLevel", rearmFreeLevel), 0f, 1f);
        rearmFreeHold = Mathf.Clamp(GetFloat("-fallReflexRearmFreeHold", rearmFreeHold), 0f, 2f);
        rearmHold = Mathf.Clamp(GetFloat("-fallReflexRearmHold", rearmHold), 0.05f, 2f);
        rearmDriftEps = Mathf.Clamp(GetFloat("-fallReflexRearmDriftEps", rearmDriftEps), 0f, 1f);
        rearmMax = Mathf.Clamp(Mathf.RoundToInt(GetFloat("-fallReflexRearmMax", rearmMax)), 1, 20);
        rearmLeadT = Mathf.Clamp(GetFloat("-fallReflexRearmLead", rearmLeadT), 0f, 1.5f);
        diveArm = GetFloat("-fallReflexDive", diveArm ? 1f : 0f) > 0.5f;
        diveVel = Mathf.Clamp(GetFloat("-fallReflexDiveVel", diveVel), 0.05f, 5f);
        diveFrames = Mathf.Clamp(Mathf.RoundToInt(GetFloat("-fallReflexDiveFrames", diveFrames)), 1, 120);
        capGuard = GetFloat("-fallReflexCapGuard", capGuard ? 1f : 0f) > 0.5f;
        capMargin = Mathf.Clamp(GetFloat("-fallReflexCapMargin", capMargin), 0f, 1f);
        brakeArm = GetFloat("-fallReflexBrake", brakeArm ? 1f : 0f) > 0.5f;
        brakeVel = Mathf.Clamp(GetFloat("-fallReflexBrakeVel", brakeVel), 0.05f, 5f);
        brakeM = Mathf.Clamp(GetFloat("-fallReflexBrakeM", brakeM), 0f, 0.30f);
        brakeHold = Mathf.Clamp(GetFloat("-fallReflexBrakeHold", brakeHold), 0f, 1f);
        brakeDir = GetFloat("-fallReflexBrakeDir", brakeDir) >= 0f ? 1f : -1f;
        brakeSide = Mathf.Clamp(Mathf.RoundToInt(GetFloat("-fallReflexBrakeSide",
            (float)brakeSide)), 0, 1);
        rearmGrace = Mathf.Clamp(GetFloat("-fallReflexRearmGrace", rearmGrace), 0f, 3f);
        railArm = GetFloat("-fallReflexRearmRail", railArm ? 1f : 0f) > 0.5f;
        railBand = Mathf.Clamp(GetFloat("-fallReflexRearmRailBand", railBand), 0.5f, 20f);
        railMargin = Mathf.Clamp(GetFloat("-fallReflexRearmRailMargin", railMargin), -1f, 2f);
        armHold = GetFloat("-fallReflexArmHold", armHold ? 1f : 0f) > 0.5f;
        armHoldMax = Mathf.Clamp(GetFloat("-fallReflexArmHoldMax", armHoldMax), 0.1f, 5f);
        XiFireTime = -1f;
        UnstepHandoverTime = -1f;
        UnwindStartTime = -1f;
        UnwindDoneTime = -1f;
        UnstepStartTime = -1f;
        UnstepDoneTime = -1f;
    }

    void Start()
    {
        balance = GetComponent<BalanceController>();
        state = GetComponent<BodyStateEstimator>();
        vest = GetComponent<VestibularSystem>();
        if (balance == null || state == null)
        {
            Debug.LogWarning("FALLREFLEX: no BalanceController/BodyStateEstimator on " + name + " - inert");
            enabled = false;
            return;
        }
        neck = FindDrive("Neck");
        head = FindDrive("Head");
        arms = GetComponent<ArmDrive>();
        // Round 10: the base stance as captured at Start - the un-step's homes. Read-only.
        hip0L = JointAngleOf("LeftLegThigh");
        hip0R = JointAngleOf("RightLegThigh");
        knee0L = JointAngleOf("LeftLegShin");
        knee0R = JointAngleOf("RightLegShin");
        xL0 = state.leftFootPosition.x;
        xR0 = state.rightFootPosition.x;
        unstepSoles[0] = SegmentCollider("LeftLegFoot");
        unstepSoles[1] = SegmentCollider("LeftLegToe");
        unstepSoles[2] = SegmentCollider("RightLegFoot");
        unstepSoles[3] = SegmentCollider("RightLegToe");
        unstepFilter = new ContactFilter2D();
        unstepFilter.SetLayerMask(GroundLayers.Mask);
        unstepFilter.useTriggers = false;
        unstepFilterReady = true;
        // Round 20: the ankles, for the xi arm's CoM height (the GeometricFallState reference).
        Transform xiTl = transform.Find("LeftLegFoot");
        if (xiTl != null) xiAnkleL = xiTl.GetComponent<HingeJoint2D>();
        Transform xiTr = transform.Find("RightLegFoot");
        if (xiTr != null) xiAnkleR = xiTr.GetComponent<HingeJoint2D>();
        if (!armed)
        {
            enabled = false;
            return;
        }
        Debug.Log("FALLREFLEX armed margin=" + F(fireMargin) + " frames=" + confirmFrames
            + " arm=" + F(shoulderDeg) + "/" + F(elbowDeg) + " hip=" + F(hipDeg) + " knee=" + F(kneeDeg));
        if (xiArm)
            Debug.Log("FALLREFLEX XI armed frames=" + xiFrames + " lead=" + F(xiLead) + " eps=" + F(xiEps));
        if (rearmEnabled || fallingGate || armHold || diveArm || capGuard || brakeArm || railArm
            || rearmGrace > 0f)
            Debug.Log("FALLREFLEX REENGAGE armed rearm=" + (rearmEnabled ? 1 : 0)
                + " margin=" + F(rearmMargin) + " frames=" + rearmFrames
                + " cooldown=" + F(rearmCooldown) + " stepHold=" + F(rearmStepHold)
                + " free=" + F(rearmFreeLevel) + "/" + F(rearmFreeHold) + " max=" + rearmMax
                + " lead=" + F(rearmLeadT)
                + " fallingGate=" + (fallingGate ? 1 : 0) + "/" + F(fallingGateMargin)
                + " armHold=" + (armHold ? 1 : 0) + "/" + F(armHoldMax)
                + " dive=" + (diveArm ? 1 : 0) + "/" + F(diveVel) + "x" + diveFrames
                + " capGuard=" + (capGuard ? 1 : 0) + "/" + F(capMargin)
                + " brake=" + (brakeArm ? 1 : 0) + "/" + F(brakeVel) + "x" + F(brakeM)
                + "h" + F(brakeHold) + "d" + F(brakeDir) + "s" + brakeSide
                + " grace=" + F(rearmGrace)
                + " rail=" + (railArm ? 1 : 0) + "/" + F(railBand) + "m" + F(railMargin));
        if (railArm)
            Debug.Log("FALLREFLEX RAILRESEAT armed band=" + F(railBand)
                + " margin=" + F(railMargin));
    }

    private static string F(float v)
    {
        return v.ToString("0.###", CultureInfo.InvariantCulture);
    }

    // Round 27: is the ankle within band degrees of one of its own limits? (side-independent)
    private static bool AnkleAtStop(HingeJoint2D j, float band)
    {
        if (j == null || !j.useLimits) return false;
        float a = j.jointAngle;
        return a >= j.limits.max - band || a <= j.limits.min + band;
    }

    void FixedUpdate()
    {
        float dt = Time.fixedDeltaTime;
        if (!orientTried)
        {
            orientTried = true;
            orient = GetComponent<PoseOrientation>();
        }
        headBiasNow = headBiasDeg;

        if (!latched)
        {
            // CYCLE 120 - THE WALK-ON GUARD (see the fields): while the settle
            // release's window lives, a fire may not start a new swing until the
            // body reads quiet; the cap (armed value) ends the freeze regardless.
            bool settleGuarded = false;
            if (balance != null && balance.settleReleaseGuardT > 0f)
            {
                if (!settleGuardSeen)
                {
                    settleGuardSeen = true;
                    settleGuardT = 0f;
                    settleGuardAge = 0f;
                    if (logTrace)
                        Debug.Log("FALLREFLEX guard watch t=" + F(Time.time)
                            + " w=" + F(balance.settleReleaseGuardT));
                }
                balance.settleReleaseGuardT -= dt;
                settleGuardAge += dt;
                bool quietNow = state != null && state.leftFootGrounded && state.rightFootGrounded
                    && Mathf.Abs(state.comVelocity.x) <= settleGuardMps
                    && state.supportMargin >= settleGuardMargin;
                settleGuardT = quietNow ? settleGuardT + dt : 0f;
                if (settleGuardT >= settleGuardQuiet)
                {
                    balance.settleReleaseGuardT = 0f;
                    if (logTrace)
                        Debug.Log("FALLREFLEX guard clear t=" + F(Time.time)
                            + " why=quiet age=" + F(settleGuardAge));
                }
                else if (balance.settleReleaseGuardT <= 0f)
                {
                    if (logTrace)
                        Debug.Log("FALLREFLEX guard clear t=" + F(Time.time)
                            + " why=cap age=" + F(settleGuardAge));
                }
                else
                {
                    settleGuarded = true;
                }
            }
            else
            {
                settleGuardSeen = false;
            }
            // ── fire test ──
            // Round 22: THE FALLING GATE. The old entrance was Standing-only, so a release that
            // lands mid-topple can never re-fire while the body reads Falling (the -35 topple
            // reads Falling ~0.5 s before its criterion; the r21 release->refire only worked
            // because the release frame still read Standing). With -fallReflexFallingGate 1 the
            // entrance also opens from Falling while the margin is below the gate floor. The
            // margin/xi arms keep their own thresholds inside. Default 0 = the old gate.
            bool gateStanding = balance.locomotionState == BalanceController.LocomotionState.Standing;
            bool gateFalling = fallingGate
                && balance.locomotionState == BalanceController.LocomotionState.Falling
                && state.supportMargin < fallingGateMargin;
            if (forceTime >= 0f && !forceDone && Time.time >= forceTime && gateStanding)
            {
                forceDone = true;
                Fire();
            }
            else if (gateStanding || gateFalling)
            {
                // The gate (29.09, priced on the fences): a margin cross alone trips on a
                // landing - the drop's airborne arrival crosses -0.06 with BOTH feet up, and
                // its post-landing wobble dips to -0.10 for 0.035 s. Requiring a grounded
                // foot AND a 0.12 s confirm leaves the drop record's longest run at 7
                // frames, while the tilt takes' pre-fall runs are 48-50 frames: the fire
                // lands ~0.5 s before Falling. See _tilt_r3_scan.py / round 3 report.
                float tilt = vest != null ? Mathf.Abs(vest.GetBodyTilt()) : 0f;
                bool footDown = state.leftFootGrounded || state.rightFootGrounded;
                bool marginReady = footDown && tilt < 60f && state.supportMargin < fireMargin;
                // Round 20: the xi arm rides beside the margin arm - whichever trips first fires
                // the sequence. With -fallReflexXi 0 (default) xiReady stays false and the test
                // below is the old one, byte for byte.
                bool xiReady = xiArm && footDown && tilt < 60f && XiOutsideHull(dt);
                if (marginReady) marginRun++; else marginRun = 0;
                if (xiReady) xiRun++; else xiRun = 0;
                if (marginReady && marginRun >= confirmFrames)
                {
                    // CYCLE 120: the walk-on guard defers the fire (see above).
                    if (settleGuarded)
                    {
                        if (logTrace && marginRun == confirmFrames)
                            Debug.Log("FALLREFLEX guard defer t=" + F(Time.time)
                                + " arm=margin v=" + F(state.comVelocity.x) + " m=" + F(state.supportMargin));
                    }
                    else
                    {
                        fireArm = "margin";
                        Fire();
                    }
                }
                else if (xiReady && xiRun >= xiFrames)
                {
                    if (settleGuarded)
                    {
                        if (logTrace && xiRun == xiFrames)
                            Debug.Log("FALLREFLEX guard defer t=" + F(Time.time)
                                + " arm=xi v=" + F(state.comVelocity.x) + " m=" + F(state.supportMargin));
                    }
                    else
                    {
                        fireArm = "xi";
                        XiFireTime = Time.time;
                        Fire();
                    }
                }
            }
            else
            {
                marginRun = 0;
            }
            return;
        }

        // ── latched: release test (a body standing cleanly again, or the hold cap) ──
        latency += dt;
        float dt2 = Time.fixedDeltaTime;

        // ── THE SELF-ORIENTATION BRANCH (round 17, addendum 8) ──
        // The body's own pose says which reach is protective: on its back the arms
        // rest along the body (a "forward" thrust would point them off the deck -
        // the owner's evidence frame); upright and prone keep the forward-down
        // brace. The branch is live, not latched at fire: a fall that reclines
        // past the cone mid-latch flips it.
        bool rest = orient != null && orient.Valid && orient.LyingBack;
        if (rest != orientRest)
        {
            orientRest = rest;
            if (rest)
            {
                OrientRestCount++;
                if (logTrace)
                    Debug.Log("FALLREFLEX orient t=" + F(Time.time) + " pose=" + orient.StateName
                        + " branch=arms-side torso=" + F(orient.TorsoDeg));
            }
            else if (logTrace)
            {
                Debug.Log("FALLREFLEX orient t=" + F(Time.time) + " pose="
                    + (orient != null ? orient.StateName : "-") + " branch=arms-forward");
            }
        }
        if (rest) headBiasNow = orient.supineHeadBiasDeg;

        // ── THE UNWIND (round 9): the clean-stand release becomes a wind-back ramp ──
        if (unwinding)
        {
            if (balance.locomotionState != BalanceController.LocomotionState.Standing)
            {
                // THE PLANT HAND-OVER (round 11). The r10 catch aborted mid-plant the moment the
                // rig's own machinery flipped locoState while the foot was mid-flight (the drive
                // was cut at 73% of its target; a Walking/Falling transit on a swinging deck).
                // The hand-over keeps the un-step's stewardship across that transit so the foot
                // completes its plant - while the ARMS go back to the rig at once (the fall's own
                // press path must never wait on a leg placement) and a body already DOWN aborts.
                bool plantInFlight = unstepHand && unstepEnabled && unstepActivated && !unstepDone
                    && balance.locomotionState != BalanceController.LocomotionState.Fallen;
                if (plantInFlight && !unstepHandover && state.supportMargin > unstepHandFloorM)
                {
                    unstepHandover = true;
                    UnstepHandoverTime = Time.time;
                    if (armsOwned)
                    {
                        armsOwned = false;
                        ReleaseArms();
                    }
                    if (logTrace)
                        Debug.Log("FALLREFLEX unstep handover t=" + F(Time.time)
                            + " loco=" + (int)balance.locomotionState
                            + " margin=" + F(state.supportMargin)
                            + " x=" + F(FootX(unstepSide))
                            + " err=" + F(FootX(unstepSide) - unstepTargetX)
                            + " sep=" + F(Sep()));
                }
                else if (!(plantInFlight && unstepHandover))
                {
                    // The body left Standing mid-wind-back: the fall's own press path must run,
                    // so the unwind stops here and the normal sequence takes the frame.
                    unwinding = false;
                    if (logTrace)
                        Debug.Log("FALLREFLEX unwind abort t=" + F(Time.time) + " (body left Standing)");
                }
            }
            if (unwinding)
            {
                float udose = 1f - Mathf.Clamp01((Time.time - unwindStart) / unwindRamp);
                if (armsOwned && arms != null)
                {
                    arms.reflexShoulderDeg = shoulderDeg * udose;
                    arms.reflexElbowDeg = elbowDeg * udose;
                }
                // THE LEG (round 10): the un-step relocates the foot that left its home place -
                // a real step on the free foot, in place of the r9 wind-back that rotated a
                // planted leg and left the split (the measured round-9 residual). With
                // -fallReflexUnstep 0 the r9 drive runs unchanged (the fence control).
                if (unstepEnabled)
                {
                    if (!unstepDone) UpdateUnstep();
                }
                else
                {
                    if (stepHip.flexor != null)
                        DriveJointToAngle(stepHip, hipDeg * udose, balance.hipPGain, balance.hipDGain,
                            balance.errorReferenceDegrees, stepSlewPerSecond);
                    if (stepKnee.flexor != null)
                        DriveJointToAngle(stepKnee, kneeDeg * udose, balance.kneePGain, balance.kneeDGain,
                            balance.errorReferenceDegrees, stepSlewPerSecond);
                }
                DriveNeckHead(dt2, udose);
                if (udose <= 0f && (!unstepEnabled || unstepDone))
                {
                    unwinding = false;
                    latched = false;
                    marginRun = 0;
                    stepOwned = false;
                    if (armsOwned)
                    {
                        armsOwned = false;
                        ReleaseArms();
                    }
                    if (UnwindDoneTime < 0f) UnwindDoneTime = Time.time;
                    if (logTrace)
                        Debug.Log("FALLREFLEX unwind done t=" + F(Time.time)
                            + " margin=" + F(state.supportMargin));
                }
                return;
            }
        }

        bool standingAgain = balance.locomotionState == BalanceController.LocomotionState.Standing
            && state.leftFootGrounded && state.rightFootGrounded
            && state.supportMargin > fireMargin + 0.01f;
        releaseAge = standingAgain ? releaseAge + dt : 0f;
        // Round 25: THE CAP GUARD (-fallReflexCapGuard, default 0). The record's 4 s latch cap
        // released mid-catch (30.385, age 4.005, the n=5 catch still in flight). Guarded, the cap
        // releases only with no catch in flight (no open episode, no reflex step airborne) and
        // the margin clear - or the body already down (the get-up's business). Deferrals are
        // counted so the guard can never hang silently. Off: capOk stays true and the test below
        // is the old one, byte for byte.
        bool capHit = holdSeconds > 0f && latency >= holdSeconds;
        bool capOk = true;
        if (capGuard && capHit)
        {
            bool catchBusy = rearmEpisode || (stepOwned && stepLandedAt < 0f);
            capOk = balance.locomotionState == BalanceController.LocomotionState.Fallen
                || (!catchBusy && state.supportMargin >= capMargin);
            if (!capOk)
            {
                capDefer++;
                if (capFirstDeferT < 0f) capFirstDeferT = Time.time;
                if (capDefer == 1 && logTrace)
                    Debug.Log("FALLREFLEX cap defer t=" + F(Time.time) + " age=" + F(latency)
                        + " margin=" + F(state.supportMargin));
            }
        }
        if ((standingAgain && releaseAge >= unwindGateSeconds) || (capHit && capOk))
        {
            if (unwindEnabled && standingAgain)
            {
                // The recovery is real: instead of snapping the arms and keeping the caught
                // leg stepped (the r4 permanence), wind every channel back over unwindRamp.
                unwinding = true;
                unwindStart = Time.time;
                UnwindCount++;
                rearmEpisode = false;
                rearmRun = 0;
                // A fresh wind-back gets a fresh un-step (the previous recovery's placement stands).
                unstepActivated = false;
                unstepDone = false;
                unstepArrived = false;
                unstepFreeSeen = false;
                unstepSettle = 0f;
                unstepHandover = false;
                unstepStart = -1f;
                if (UnwindStartTime < 0f) UnwindStartTime = Time.time;
                if (logTrace)
                    Debug.Log("FALLREFLEX unwind start t=" + F(Time.time)
                        + " margin=" + F(state.supportMargin) + " age=" + F(latency));
                return;
            }
            latched = false;
            marginRun = 0;
            ReleaseArms();
            stepOwned = false;
            rearmEpisode = false;
            rearmRun = 0;
            if (logTrace)
                Debug.Log("FALLREFLEX release t=" + F(Time.time) + " age=" + F(latency));
            return;
        }

        // ── THE RE-STATION RE-ARM (round 22; the r21 addendum + [[CONSULT-0930-0459]] Q1) ──
        // The r21 record: the first catch recovers the body (margin back to +0.2), the deck then
        // reverses and the man topples over the foot the first answer planted - there is NO
        // RE-STATION: one fire per latch, the second breach runs unanswered (fell 29.055; the
        // release->refire route fell 29.195 with a REAR catch the reversal overturned). This
        // starts a fresh foot-placement episode while the latch holds, on a margin / capture-point
        // re-breach, once a recovery window has been re-earned (the first breach of the take,
        // where the reflex's own step is mid-answer, must not double-step). The placement law
        // gains the FRONT catch (UpdateUnstep): the CoM side picks the side.
        if (rearmEnabled)
        {
            if (state.supportMargin >= rearmFreeLevel)
            {
                rearmFreeAge += dt;
                if (rearmFreeAge >= rearmFreeHold) rearmFreed = true;
            }
            else
            {
                rearmFreeAge = 0f;
            }
            if (!rearmEpisode && rearmCount < rearmMax && rearmFreed
                && (balance.locomotionState == BalanceController.LocomotionState.Standing
                    || balance.locomotionState == BalanceController.LocomotionState.Falling))
            {
                float tiltR = vest != null ? Mathf.Abs(vest.GetBodyTilt()) : 0f;
                bool footDownR = state.leftFootGrounded || state.rightFootGrounded;
                bool stepBusy = stepOwned && stepLandedAt < 0f && latency < rearmStepHold;
                // Round 25: THE DIVE TRIP. The -35 tail dive (com 0.37 -> 1.19 m/s over 0.4 s)
                // ran with the catch chain idle for 0.265 s - the margin/xi arms only saw it at
                // its end. |comVelocity.x| >= diveVel confirmed for diveFrames starts a fresh
                // catch the moment the run-away begins. diveReady is false without
                // -fallReflexDive, so `trip` below is the old test, bit for bit.
                bool diveReady = diveArm && footDownR && tiltR < 60f
                    && Mathf.Abs(state.comVelocity.x) >= diveVel;
                if (diveReady) diveRun++; else diveRun = 0;
                bool diveTrip = diveReady && diveRun >= diveFrames;
                // Same short-circuit order and the same XiOutsideHull call count as before:
                // margin false and the body standable -> xi evaluated exactly once.
                bool marginTrip = state.supportMargin < rearmMargin;
                bool xiTripBit = false;
                if (footDownR && tiltR < 60f && !marginTrip)
                    xiTripBit = xiArm && XiOutsideHull(dt);
                // Round 27: THE ANKLE-RAIL RE-SEAT (balance-refs item 3(b); the r24 wall's bind:
                // "the CoP has no more travel" once the ankle rails). While the margin is tight,
                // either ankle sitting within railBand of its own stop starts a fresh placement -
                // a re-seat restores that foot's flat follow pose and its push range. railTrip is
                // false without -fallReflexRearmRail, so `trip` below is the old test, bit for bit.
                bool railReady = railArm && footDownR && tiltR < 60f
                    && state.supportMargin <= railMargin;
                bool railTrip = railReady
                    && (AnkleAtStop(xiAnkleL, railBand) || AnkleAtStop(xiAnkleR, railBand));
                bool trip = footDownR && tiltR < 60f
                    && (marginTrip || xiTripBit || diveTrip || railTrip);
                if (trip) rearmRun++; else rearmRun = 0;
                float lastStepT = stepLandedAt > 0f ? stepLandedAt : fireTime;
                float coolBase = Mathf.Max(rearmLastEnd, lastStepT);
                bool cooled = Time.time - coolBase >= rearmCooldown;
                // Round 26: THE ARRIVAL GRACE (-fallReflexRearmGrace, default 0 = off, byte for
                // byte). The r25 d45f6 price: a fresh episode chained 0.02 s after an arrival and
                // grabbed the just-planted support right before the criterion. With grace > 0 a
                // new episode also needs this long since the last ARRIVAL.
                bool graceOk = rearmGrace <= 0f || rearmLastArriveT < 0f
                    || Time.time - rearmLastArriveT >= rearmGrace;
                if (rearmRun >= rearmFrames && !stepBusy && cooled && graceOk)
                {
                    rearmRun = 0;
                    rearmCount++;
                    rearmEpisode = true;
                    rearmArriveT = -1f;
                    if (diveTrip) diveCount++;
                    if (railTrip)
                    {
                        railCount++;
                        if (railFirstT < 0f) railFirstT = Time.time;
                    }
                    diveRun = 0;
                    stepOwned = false;       // the re-arm takes the legs; one writer per joint
                    unstepActivated = false;
                    unstepDone = false;
                    unstepArrived = false;
                    unstepFreeSeen = false;
                    unstepSettle = 0f;
                    unstepHandover = false;
                    unstepStart = -1f;
                    if (logTrace)
                        Debug.Log("FALLREFLEX REARM t=" + F(Time.time)
                            + " n=" + rearmCount
                            + " margin=" + F(state.supportMargin)
                            + " com=" + F(state.comPosition.x)
                            + " vel=" + F(state.comVelocity.x)
                            + " hull=" + F(state.supportMinX) + "/" + F(state.supportMaxX)
                            + " arm=" + (marginTrip ? "margin" : (xiTripBit ? "xi"
                                : (diveTrip ? "dive" : "rail")))
                            + " rail=" + (railTrip ? 1 : 0));
                }
            }
            if (rearmEpisode)
            {
                // r22c: end the episode 0.2 s after ARRIVAL. The r22b price: the settle's
                // err-band re-check dropped the arrived episode back into the wait-for-free
                // state (its foot was now the loaded stance), where it sat until the body read
                // Fallen - the whole 28.6-29.5 creep ran with NO answer (the front catch fired
                // at 29.85, past the 29.75 criterion). A placement that arrived and planted is
                // done. The earned recovery window still survives across episodes (r22b).
                if (unstepArrived && rearmArriveT < 0f)
                {
                    rearmArriveT = Time.time;
                    rearmLastArriveT = Time.time;   // round 26: the arrival grace base
                }
                bool placed = rearmArriveT > 0f && (Time.time - rearmArriveT) >= rearmHold;
                if (unstepDone || placed
                    || balance.locomotionState == BalanceController.LocomotionState.Fallen)
                {
                    rearmEpisode = false;
                    rearmLastEnd = Time.time;
                    rearmArriveT = -1f;
                }
                else
                {
                    UpdateUnstep();
                }
            }
        }

        // ── the protective sequence ──

        // HEAD: keep the rig's own world-upright neck/head PD running (the head does not
        // flop with the ragdoll); the biased target tips it slightly back.
        DriveNeckHead(dt2);

        // ARMS: both shoulders thrust forward-down (the brace). Emitted through ArmDrive's
        // own command - a muscle-level write loses the per-frame tug of war with ArmDrive's
        // re-drive (probe take tiltA_r3_probe: actShoulderL pinned at 0.100, shoulderAngleL
        // stalled at -8 deg). One writer per joint: the reflex writes the COMMAND, ArmDrive
        // keeps the muscles. Handed back to the rig the moment the body leaves Standing, so
        // the fall's own press (hands toward the ground) runs as before.
        // Round 22 (-fallReflexArmHold): the r21 consult - keep the arm brace alive through a
        // Falling pass while the catch-step is still in flight; the hand-over runs the moment
        // the step lands (or the armHoldMax window closes). Default 0 = the old one-shot hand-over.
        if (armsOwned && balance.locomotionState != BalanceController.LocomotionState.Standing
            && !(armHold && stepOwned && stepLandedAt < 0f && latency < armHoldMax))
        {
            armsOwned = false;
            ReleaseArms();
            if (logTrace)
                Debug.Log("FALLREFLEX arms->press t=" + F(Time.time));
        }
        if (armsOwned)
        {
            float armDose = Mathf.Clamp01((latency - spinalDelay) / armRamp);
            if (arms != null)
            {
                // The pose branch picks the target the dose ramps to: the forward
                // brace, or the along-the-body rest when the body is on its back.
                float shTarget = orientRest ? orient.sideShoulderDeg : shoulderDeg;
                float elTarget = orientRest ? orient.sideElbowDeg : elbowDeg;
                arms.reflexShoulderDeg = shTarget * armDose;
                arms.reflexElbowDeg = elTarget * armDose;
            }
        }

        // STEP: one leg swings forward; once the swing foot has left the deck and landed
        // again (plus 0.4 s), the leg goes back to the rig.
        if (stepOwned)
        {
            float stepDose = Mathf.Clamp01((latency - stepDelay) / stepRamp);
            if (stepDose > 0f)
            {
                DriveJointToAngle(stepHip, hipDeg * stepDose, balance.hipPGain, balance.hipDGain,
                    balance.errorReferenceDegrees, stepSlewPerSecond);
                DriveJointToAngle(stepKnee, kneeDeg * stepDose, balance.kneePGain, balance.kneeDGain,
                    balance.errorReferenceDegrees, stepSlewPerSecond);
            }
            bool swingDown = stepSide == 0 ? state.leftFootGrounded : state.rightFootGrounded;
            if (!swingDown)
                stepLeft = true;
            else if (stepLeft && stepLandedAt < 0f)
                stepLandedAt = Time.time;
            if (stepLandedAt > 0f && Time.time - stepLandedAt >= 0.4f)
            {
                stepOwned = false;
                if (logTrace)
                    Debug.Log("FALLREFLEX step landed t=" + F(Time.time));
            }
        }
    }

    // ─── ROUND 10: THE UN-STEP (the foot-placement unwind) ───
    // The r9 wind-back returned the stepped leg's JOINT angles while the foot stayed where
    // the step had put it; the take's measured residual was the split (Sep() ~0.35 m at the
    // unwind's end, round 9 report). The un-step relocates THE FOOT: pick the foot that left
    // its home x the most, wait for its free window (a LOADED foot is never dragged - the
    // rig's catch machinery owns it), fold the knee to keep it light, drive the hip by a
    // foot-x servo (deg per metre of the measured error), and let it plant at home. Any
    // give-up (never free, the watchdog) completes the unwind exactly as the r9 path did.
    private void UpdateUnstep()
    {
        float dt = Time.fixedDeltaTime;
        if (!unstepActivated)
        {
            float eL = Mathf.Abs(state.leftFootPosition.x - xL0);
            float eR = Mathf.Abs(state.rightFootPosition.x - xR0);
            // v2 (the v1 price): pulling the displaced foot toward HOME cost the margin when the
            // CoM sat BEHIND the home - the rear support edge moved forward under a drifting CoM
            // (probe: fall 25.135 vs 26.505). So when the CoM owns a base behind the homes, the
            // REARMOST foot is re-placed a gap behind the CoM - a real rear catch, the move the
            // rig's own machinery buys its recoveries with. Otherwise the v1 toward-home trim.
            float comXSel = state.comPosition.x;
            // Round 22: THE RE-STATION STEP. The r21 loss: the previous answer planted a REAR
            // catch, the deck's reversal turned the drift forward and the man toppled OVER the
            // planted foot. The r22b price: once the feet have left their homes the homes-based
            // test is degenerate (it read "rear" for every swing of that take). So on the
            // re-arm route THE DRIFT PICKS THE SIDE: drifting forward -> the FRONT-most foot
            // is re-placed to (comX + gap) - a forward catch; drifting backward -> the
            // rear-most foot to (comX - gap). With -fallReflexRearm 0 this is the r10 v2 law,
            // byte for byte (the unwind route is untouched).
            bool rearCatch;
            bool frontCatch;
            if (rearmEnabled)
            {
                // r22e: a MARGINAL drift sign is noise (0.055 s moved the r22d n=2 from the
                // front route to a wasteful rear pull-back). Only a clear backward drift goes
                // REAR; everything else goes FRONT (bounded by the no-pull-back clamp).
                bool forward = state.comVelocity.x >= -rearmDriftEps;
                rearCatch = !forward;
                frontCatch = forward;
                unstepSide = forward
                    ? (state.leftFootPosition.x >= state.rightFootPosition.x ? 0 : 1)
                    : (state.leftFootPosition.x <= state.rightFootPosition.x ? 0 : 1);
            }
            else
            {
                rearCatch = comXSel - unstepGapM < Mathf.Min(xL0, xR0);
                frontCatch = false;
                unstepSide = rearCatch ? (state.leftFootPosition.x <= state.rightFootPosition.x ? 0 : 1)
                                       : (eR >= eL ? 1 : 0);
            }
            unstepHomeX = unstepSide == 1 ? xR0 : xL0;
            // r23: THE PRED-LAND TERM (-fallReflexRearmLead, default 0 = the r22 law byte for
            // byte). During the dive the com runs at 1.0-1.5 m/s; the flight to the target eats
            // the whole standoff and the catch lands UNDER the moving body, unloaded (the r22
            // last-catch price: arrive x 0.488 vs com 0.499). With leadT > 0 the FRONT catch
            // aims (leadT x the live forward drift) beyond the instantaneous com+gap - the foot
            // lands ahead of the capture point at touchdown. FRONT branch only: the r23a price
            // (tiltA_r23_l20/l35) showed a leaded REAR catch (a deeper L landing) perturbs the
            // whole chain into a wide split that collapses ~0.65 s EARLIER; the rear keeps the
            // r22 law exactly. Re-station episodes only.
            float predLead = (rearmEpisode && rearmLeadT > 0f && frontCatch)
                ? rearmLeadT * Mathf.Max(0f, state.comVelocity.x)
                : 0f;
            // r22d: the front catch never pulls an already-ahead foot BACK (the f6c10 price: the
            // servo dragged a foot at 0.75 m back to comX+gap and the take fell 0.7 s early);
            // with the foot already ahead of (comX + gap) the target collapses onto the foot
            // itself and the episode skips - it is a front support already.
            unstepTargetX = rearCatch ? Mathf.Min(unstepHomeX, comXSel - unstepGapM)
                          : frontCatch ? Mathf.Max(unstepHomeX,
                                Mathf.Max(FootX(unstepSide), comXSel + unstepGapM + predLead))
                          : unstepHomeX;
            if (Mathf.Abs(FootX(unstepSide) - unstepTargetX) <= unstepHomeM)
            {
                unstepDone = true;
                if (logTrace)
                    Debug.Log("FALLREFLEX unstep skip t=" + F(Time.time) + " (foot already at target)");
                return;
            }
            unstepActivated = true;
            unstepStart = Time.time;
            unstepHipHomeDeg = unstepSide == 1 ? hip0R : hip0L;
            unstepKneeHomeDeg = unstepSide == 1 ? knee0R : knee0L;
            unstepHipTargetDeg = unstepHipHomeDeg;
            unstepKneeTargetDeg = unstepKneeHomeDeg;
            unstepHipHoldDeg = unstepHipHomeDeg;
            unstepKneeHoldDeg = unstepKneeHomeDeg;
            unstepHip = FindDrive(unstepSide == 1 ? "RightLegThigh" : "LeftLegThigh");
            unstepKnee = FindDrive(unstepSide == 1 ? "RightLegShin" : "LeftLegShin");
            UnstepStartTime = Time.time;
            if (logTrace)
                Debug.Log("FALLREFLEX unstep start t=" + F(Time.time)
                    + " side=" + (unstepSide == 1 ? "R" : "L")
                    + " x=" + F(FootX(unstepSide)) + " home=" + F(unstepHomeX)
                    + " target=" + F(unstepTargetX) + " err=" + F(FootX(unstepSide) - unstepTargetX)
                    + " comX=" + F(comXSel) + " rearCatch=" + (rearCatch ? 1 : 0) + " sep=" + F(Sep()));
        }

        float fz0 = SoleLoad(0);
        float fz1 = SoleLoad(1);
        float total = fz0 + fz1;
        float mine = unstepSide == 1 ? fz1 : fz0;
        float other = unstepSide == 1 ? fz0 : fz1;
        float x = FootX(unstepSide);
        float err = x - unstepTargetX;
        float age = Time.time - unstepStart;

        bool free = (total > 1f && mine < 0.40f * total) || ClearanceOf(unstepSide) > 0.012f;
        bool otherLoaded = total > 1f && other >= 0.5f * total;
        if (free) unstepFreeSeen = true;

        if (age > unstepWait)
        {
            unstepDone = true;
            UnstepDoneTime = Time.time;
            if (logTrace)
                Debug.Log("FALLREFLEX unstep giveup t=" + F(Time.time)
                    + " x=" + F(x) + " err=" + F(err) + " free=" + (free ? 1 : 0)
                    + " seen=" + (unstepFreeSeen ? 1 : 0) + " sep=" + F(Sep()));
            return;
        }

        if (Mathf.Abs(err) <= unstepHomeM)
        {
            // Arrived: hold the pose the foot landed in briefly so it plants, then finish.
            if (!unstepArrived)
            {
                unstepArrived = true;
                unstepHipHoldDeg = unstepHipTargetDeg;
                unstepKneeHoldDeg = unstepKneeTargetDeg;
                brakeEngaged = false;
                brakeOtherReady = false;
                if (logTrace)
                    Debug.Log("FALLREFLEX unstep arrive t=" + F(Time.time) + " x=" + F(x)
                        + " target=" + F(unstepTargetX) + " hipHold=" + F(unstepHipHoldDeg)
                        + " sep=" + F(Sep()));
            }
            unstepSettle += dt;
            // ROUND 26: THE ARRIVED PRESS (the pre-dive brake; -fallReflexBrake, default 0 - off
            // is the old path byte for byte). The r25 wall: the -35 tail dive (0.3 -> 1.4 m/s
            // over ~0.5 s) runs while the just-landed catch HOLDS; r25 priced the triggers and
            // the flight, never a press. While the CoM still coasts (|comVelX| >= brakeVel) the
            // arrived hold presses the support up-slope under the body: the same foot-x servo
            // (deg-per-metre gain) that placed the foot is aimed brakeM ahead of where it landed
            // - a planted foot cannot follow, the pelvis takes the reaction. Side 1 presses the
            // OTHER (loaded) leg - the one with authority; side 0 the just-arrived one.
            // brakeHold extends the hold so the press has time (0 = the plain 0.15 s settle).
            bool brakeOn = false;
            float hipHoldNow = unstepHipHoldDeg;
            if (brakeArm && Mathf.Abs(state.comVelocity.x) >= brakeVel)
            {
                brakeOn = true;
                float pressErr = -brakeDir * brakeM;
                if (brakeSide == 1)
                {
                    if (!brakeOtherReady)
                    {
                        brakeOtherReady = true;
                        brakeOtherHip = FindDrive(unstepSide == 1 ? "LeftLegThigh" : "RightLegThigh");
                        brakeOtherHipDeg = JointAngleOf(unstepSide == 1 ? "LeftLegThigh"
                            : "RightLegThigh");
                    }
                    if (brakeOtherHip.flexor != null)
                        DriveJointToAngle(brakeOtherHip, brakeOtherHipDeg + unstepGainDeg * pressErr,
                            balance.hipPGain, balance.hipDGain, balance.errorReferenceDegrees,
                            stepSlewPerSecond);
                }
                else
                {
                    hipHoldNow = unstepHipHoldDeg + unstepGainDeg * pressErr;
                }
                if (!brakeEngaged)
                {
                    brakeEngaged = true;
                    brakeCount++;
                    if (logTrace)
                        Debug.Log("FALLREFLEX brake t=" + F(Time.time)
                            + " side=" + (brakeSide == 1 ? "B" : "A")
                            + " leg=" + (unstepSide == 1 ? "R" : "L")
                            + " v=" + F(state.comVelocity.x) + " m=" + F(brakeM)
                            + " hip=" + F(hipHoldNow) + " x=" + F(x));
                }
            }
            if (unstepHip.flexor != null)
                DriveJointToAngle(unstepHip, hipHoldNow, balance.hipPGain, balance.hipDGain,
                    balance.errorReferenceDegrees, stepSlewPerSecond);
            if (unstepKnee.flexor != null)
                DriveJointToAngle(unstepKnee, unstepKneeHoldDeg, balance.kneePGain, balance.kneeDGain,
                    balance.errorReferenceDegrees, stepSlewPerSecond);
            float settleNeed = unstepSettleS + (brakeOn ? brakeHold : 0f);
            if (unstepSettle >= settleNeed)
            {
                unstepDone = true;
                UnstepDoneTime = Time.time;
                if (logTrace)
                    Debug.Log("FALLREFLEX unstep done t=" + F(Time.time)
                        + " x=" + F(x) + " err=" + F(err) + " sep=" + F(Sep()));
            }
            return;
        }
        unstepSettle = 0f;

        // The lift half: while the foot still carries load and the other leg holds the body,
        // fold the knee to make it light (the fold also drags the foot back a little).
        if (!free && age < unstepFoldS && otherLoaded)
        {
            if (unstepKnee.flexor != null)
                DriveJointToAngle(unstepKnee, unstepKneeHomeDeg + unstepFoldDeg,
                    balance.kneePGain, balance.kneeDGain, balance.errorReferenceDegrees, stepSlewPerSecond);
            return;
        }
        if (!free)
            return;   // never drag a loaded foot; wait for the free window (the watchdog caps it)

        // The drive: the hip servo on the measured foot-x error (the step's own sign
        // convention - minus = forward, so a foot ahead of home gets a positive hip target).
        float hipT = unstepHipHomeDeg + unstepGainDeg * err;
        float kneeT = unstepKneeHomeDeg + unstepFoldDeg * Mathf.Clamp01(Mathf.Abs(err) / 0.10f);
        unstepHipTargetDeg = hipT;
        unstepKneeTargetDeg = kneeT;
        if (unstepHip.flexor != null)
            DriveJointToAngle(unstepHip, hipT, balance.hipPGain, balance.hipDGain,
                balance.errorReferenceDegrees, stepSlewPerSecond);
        if (unstepKnee.flexor != null)
            DriveJointToAngle(unstepKnee, kneeT, balance.kneePGain, balance.kneeDGain,
                balance.errorReferenceDegrees, stepSlewPerSecond);
        if (logTrace)
        {
            unstepLogClock -= dt;
            if (unstepLogClock <= 0f)
            {
                unstepLogClock = 0.2f;
                Debug.Log("FALLREFLEX unstep t=" + F(Time.time)
                    + " x=" + F(x) + " err=" + F(err) + " hipT=" + F(hipT)
                    + " fz=" + F(mine) + "/" + F(total)
                    + " clear=" + F(ClearanceOf(unstepSide)) + " sep=" + F(Sep()));
            }
        }
    }

    private float FootX(int side)
    {
        return side == 1 ? state.rightFootPosition.x : state.leftFootPosition.x;
    }

    private float ClearanceOf(int side)
    {
        return side == 1 ? state.rightFootGroundClearance : state.leftFootGroundClearance;
    }

    private float Sep()
    {
        return state.rightFootPosition.x - state.leftFootPosition.x;
    }

    private float JointAngleOf(string segmentName)
    {
        Transform t = transform.Find(segmentName);
        HingeJoint2D j = t != null ? t.GetComponent<HingeJoint2D>() : null;
        return j != null ? j.jointAngle : 0f;
    }

    private Collider2D SegmentCollider(string segmentName)
    {
        Transform t = transform.Find(segmentName);
        return t != null ? t.GetComponent<Collider2D>() : null;
    }

    // The same read the recorder's fzL/fzR columns use: the solver's contact impulses on
    // the two sole plates of one side, upward part only.
    private float SoleLoad(int side)
    {
        if (!unstepFilterReady) return 0f;
        float fz = 0f;
        float dt = Time.fixedDeltaTime;
        if (dt <= 0f) dt = 0.005f;
        for (int i = side * 2; i < side * 2 + 2; i++)
        {
            Collider2D col = unstepSoles[i];
            if (col == null) continue;
            int n = col.GetContacts(unstepFilter, unstepContacts);
            int count = Mathf.Min(n, unstepContacts.Length);
            for (int k = 0; k < count; k++)
            {
                ContactPoint2D cp = unstepContacts[k];
                float vertical = cp.normalImpulse * cp.normal.y;
                if (vertical > 0f) fz += vertical / dt;
            }
        }
        return fz;
    }

    private void ReleaseArms()
    {
        if (arms == null) return;
        arms.reflexShoulderDeg = float.NaN;
        arms.reflexElbowDeg = float.NaN;
    }

    private void Fire()
    {
        latched = true;
        latency = 0f;
        releaseAge = 0f;
        fireTime = Time.time;
        rearmRun = 0;
        rearmFreed = false;
        rearmFreeAge = 0f;
        rearmEpisode = false;
        rearmArriveT = -1f;
        rearmCount = 0;
        armsOwned = true;
        stepOwned = true;
        stepLeft = false;
        stepLandedAt = -1f;
        // The stepping leg: the foot that is NOT carrying the body goes first (the unloaded
        // foot is already free to swing); both down or both up -> the right leg.
        bool leftDown = state.leftFootGrounded;
        bool rightDown = state.rightFootGrounded;
        stepSide = (rightDown && !leftDown) ? 0 : 1;
        stepHip = FindDrive(stepSide == 0 ? "LeftLegThigh" : "RightLegThigh");
        stepKnee = FindDrive(stepSide == 0 ? "LeftLegShin" : "RightLegShin");
        if (logTrace)
            Debug.Log("FALLREFLEX fire t=" + F(Time.time) + " margin=" + F(state.supportMargin)
                + " tilt=" + F(vest != null ? vest.GetBodyTilt() : 0f)
                + " pose=" + (orient != null && orient.Valid ? orient.StateName : "-")
                + " leg=" + (stepSide == 0 ? "L" : "R")
                + " footDown=" + ((leftDown || rightDown) ? 1 : 0)
                + " arm=" + fireArm
                + (xiArm ? (" xi=" + F(xiLast) + " hull=" + F(xiHullMinLast) + "/" + F(xiHullMaxLast)) : ""));
    }

    // ─── the rig's own laws, replicated for the latched window ───

    // ControlSegmentToWorldUpright, the neck/head half (BalanceController:11651).
    private void DriveNeckHead(float dt)
    {
        DriveNeckHead(dt, 1f);
    }

    // biasScale < 1 winds the head bias back out (the unwind); 1 = the full reflex pose.
    private void DriveNeckHead(float dt, float biasScale)
    {
        if (vest == null) return;
        DriveSegmentToWorldUpright(neck, vest.GetNeckTilt(), vest.GetNeckAngularVelocity(),
            balance.neckTargetTilt, dt);
        DriveSegmentToWorldUpright(head, vest.GetHeadTilt(), vest.GetHeadAngularVelocity(),
            balance.neckTargetTilt + headBiasNow * biasScale, dt);
    }

    private void DriveSegmentToWorldUpright(Drive d, float tilt, float angVel, float target, float dt)
    {
        if (d.flexor == null || d.extensor == null || d.joint == null) return;
        float tiltForError = tilt;
        float damping = 0f;
        if (balance.useStablePd)
        {
            tiltForError = tilt + angVel * dt;
            if (d.inertia <= 0f) d.inertia = InertiaOf(d.joint);
            if (d.inertia > 0f)
            {
                float k = balance.neckDGain * d.flexor.maxTorque * Mathf.Rad2Deg
                    / Mathf.Max(1f, balance.speedReferenceDegPerSec);
                damping = k * dt / d.inertia;
            }
        }
        float error = Mathf.DeltaAngle(tiltForError, target) / Mathf.Max(1f, balance.neckErrorReferenceDegrees);
        float speed = angVel / Mathf.Max(1f, balance.speedReferenceDegPerSec);
        float raw = (-balance.neckPGain) * error - (-balance.neckDGain) * speed;
        float signal = Mathf.Clamp(raw / (1f + damping), -1f, 1f);
        ApplySignal(signal, d.flexor, d.extensor, dt, 0f);
    }

    // ControlJointToAngleStable (BalanceController:12665), the generic joint half.
    private void DriveJointToAngle(Drive d, float target, float pGain, float dGain, float errorRef,
        float slewPerSecond = 0f)
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
        ApplySignal(signal, d.flexor, d.extensor, dt, slewPerSecond);
    }

    // ApplySignal (BalanceController:10783) + UpdateMuscle slew:
    // signal >= 0 -> the extensor carries it; signal < 0 -> the flexor does.
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
        muscle.activation = Mathf.MoveTowards(
            muscle.activation, targetActivation,
            speed * dt);
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

    // ─── round 20: THE XI ARM'S OWN TEST ───
    // xi = com + v*sqrt(h/g) (BodySupportGeometry.CapturePoint). h is the CoM height above the
    // ankle hinge line - the same reference BalanceController.GeometricFallState uses (~4 % off
    // the sole-referenced recorder column); the hull is the estimator's grounded-collider span
    // (supportMinX/supportMaxX). With xiLead > 0 the comparison rides the edge predicted at
    // +lead from the edge's own EMA velocity - the MOVING-HULL term of the refs; xi itself is
    // not extrapolated (its velocity term is already inside it). eps is the ladder's 5 mm band.
    private bool XiOutsideHull(float dt)
    {
        float footSum = 0f;
        int footCount = 0;
        if (xiAnkleL != null)
        {
            footSum += xiAnkleL.transform.TransformPoint(xiAnkleL.anchor).y;
            footCount++;
        }
        if (xiAnkleR != null)
        {
            footSum += xiAnkleR.transform.TransformPoint(xiAnkleR.anchor).y;
            footCount++;
        }
        float h = footCount > 0 ? state.comPosition.y - footSum / footCount : 0f;
        float xi = BodySupportGeometry.CapturePoint(state.comPosition.x, state.comVelocity.x, h);
        float minX = state.supportMinX;
        float maxX = state.supportMaxX;
        if (xiEdgeInit && dt > 0f)
        {
            float k = Mathf.Clamp01(dt / Mathf.Max(dt, xiEdgeTau));
            xiEdgeVelMin += ((minX - xiEdgePrevMin) / dt - xiEdgeVelMin) * k;
            xiEdgeVelMax += ((maxX - xiEdgePrevMax) / dt - xiEdgeVelMax) * k;
        }
        xiEdgeInit = true;
        xiEdgePrevMin = minX;
        xiEdgePrevMax = maxX;
        float minL = minX + xiLead * Mathf.Clamp(xiEdgeVelMin, -2f, 2f);
        float maxL = maxX + xiLead * Mathf.Clamp(xiEdgeVelMax, -2f, 2f);
        xiLast = xi;
        xiHullMinLast = minX;
        xiHullMaxLast = maxX;
        return xi < minL - xiEps || xi > maxL + xiEps;
    }

    private static bool HasArg(string name)
    {
        string[] args = LauncherArgs.Args();
        return System.Array.IndexOf(args, name) >= 0;
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
