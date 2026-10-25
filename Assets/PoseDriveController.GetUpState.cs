using System.Globalization;
using UnityEngine;

// PoseDriveController: the get-up ladder's and the hand grip's state, doses and switches.
public partial class PoseDriveController
{
    // ─── get-up and hand grip ───
    // -poseGetUp 1: while Fallen with the walk key held, the body drives a rise (TickGetUp). Default 0.
    // -poseHandGrip 1: a palm pressing the slab grips (friction x HandGripMu); Hold = release delay, s.
    private bool getUpEnabled;
    private bool handGripEnabled;
    private float handGripHoldSeconds = 0.20f;
    // Stand bar on torso tilt, deg (torsoTilt sign: minus = forward); standing needs tilt above it.
    // Published to BalanceController so the JSON's getUpStandTiltDeg names the bar in use.
    private float getUpStandTiltDeg = -20f;
    // Scales the ladder's stage bounds and progress ramps. 1 = the nominal 10 s budget (flat slab);
    // raise it where the palms find the surface late (e.g. on the rocker).
    private float getUpPatience = 1f;

    // ─── fall guard ───
    // Live while support geometry >= Critical AND the pelvis descends (pair held Hold s), until IsDown.
    // ArmDrive plants the palms on the slab ahead of the shoulder; the elbows yield. Off by default.
    private bool fallGuardEnabled;
    private bool fallGuardLive;
    private float fallGuardStateAge;
    private float fallGuardFacing = 1f;
    // The trigger's descent term, m/s (pelvis vertical velocity, up = +). The walk's own balance bumps
    // stay within +-0.15 m/s, so they cannot arm the guard the way the support ladder alone would.
    private const float FallGuardDescendMetersPerSecond = -0.20f;
    // How long the trigger pair must hold before the guard arms, s (-poseFallGuardHold).
    private float fallGuardHoldSeconds = 0.10f;
    // Palm plant point ahead of the shoulder, m (-poseFallGuardReach); 0 = straight under it.
    // For scale: the arm chord linkL1 + linkL2 = 0.67 m.
    private float fallGuardReachMeters = 0.20f;
    // Get-up palm plant point ahead of the shoulder, m (push-up stance; wanted band 0.2-0.35 m).
    // Signed by the fall's facing in TickGetUp. 0 = the nearest-reachable fold under the shoulder,
    // which plants the hands at the navel.
    private float getUpPlantReachMeters = 0.35f;
    // Press request while the guard is live (ArmDrive clamps 0..1): full strength.
    private const float FallGuardPressLevel = 1f;

    // ─── get-up ladder and grip state ───
    // TickGetUp names each stage from measured signals (palm contact and load, CoM height, feet).
    // No stage advances on a timer alone; each bound (a share of the 10 s window) reports a failure.
    private enum GetUpStage { Idle = 0, Plant = 1, Press = 2, Tuck = 3, Rise = 4, Roll = 5 }
    private GetUpStage getUpStage = GetUpStage.Idle;
    private float getUpStageEnterTime;
    private float getUpDownSince = -1f;
    private float getUpFallTime = -1f;
    private float getUpRetryAfter = -1f;
    private bool getUpDone;
    private bool getUpHandsPlanted;    // latched: both palms held real contact
    private bool getUpReachedTuck;     // latched: both feet came under the body
    private bool getUpStandNamed;      // the card's bars named on one frame
    private bool getUpDriveOn;         // the law owned the joints on this step
    // Roll stage: a body on its back is rolled off it before the plant/press may engage.
    // -poseGetUpRoll 0 disables it; -poseGetUpRollForce 1 forces one roll at the first attempt (probe).
    private bool getUpRollEnabled;
    private float getUpRollForce;
    private PoseOrientation getUpOrient;   // the self-orientation core (may be absent)
    private bool getUpOrientTried;
    // Dwell: only a body read on its back continuously this long is rolled (a mid-toss pass is not).
    // Cap: after this many consecutive bound-ended rolls, stop rolling until the pose leaves the back.
    private float getUpRollDwell;
    private int getUpRollCap;
    // -poseGetUpStand 1: the rise first holds the fold (soles ride under the body while it settles),
    // then converges the legs on -poseGetUpStandHip/Knee/Ankle. Unwinding to the landed pose instead
    // sweeps the soles behind the CoM, so the feet-under bar never arms. 0 = the landed-pose unwind.
    private bool getUpStandMode;
    private float getUpStandHold;      // share of the rise spent holding the fold
    private float getUpStandHipDeg;    // the stand pose the rise converges on
    private float getUpStandKneeDeg;
    private float getUpStandAnkleDeg;
    private int getUpStandFrom;        // R29 v2: 0 = blend from the fold (draw 1),
                                       //          1 = blend from the latched pose (draw 0)
                                       // -poseGetUpStandGate 1: the Rise clock (blend, trunk, bound) holds until both soles are grounded,
                                       // so the extension runs over a planted base; StandGateMax caps the held seconds. 0 = off.
    private bool getUpStandGate;
    private float getUpStandGateMax = 3f;
    private float getUpStandGateHeld;    // seconds held inside this attempt's Rise
    private int getUpStandGateHolds;     // hold episodes inside this attempt's Rise
    private bool getUpStandGateInHold;   // a hold is open right now
    // -poseGetUpBase 1, base = feet under and grounded with palms planted: (a) Press/Tuck start the
    // Rise as soon as it reads; (b) a Tuck past its bound waits for it (BaseMax aborts as a failure);
    // (c) while waiting, the press drops to BasePress - the stroke lifts the body off its feet.
    private bool getUpBaseOn;
    private float getUpBaseMax = 3f;      // the Tuck wait's cap, seconds
    private float getUpBasePress;         // the press level while the gate holds
    // -poseGetUpWaitPress P (Sec S): during a wait the press ramps 0 -> P over S s per episode, so the
    // arms lift a pelvis-seated body toward its feet (unloaded soles give the legs nothing to push).
    // -1 = off; GETUP-WAITPRESS logs the sole fz response.
    private float getUpWaitPress = -1f;    // the wait's lift level (-1 = off)
    private float getUpWaitPressSec = 1.2f;// the lift's ramp, seconds
    private float getUpWaitPressT;         // seconds of lift this wait episode
    private float getUpWaitPressLogT;      // the next periodic log, seconds
    private float getUpBaseDraw = 1f;     // the re-seat draw while the Tuck waits
    private float getUpBaseWait;          // seconds waited for the base this attempt
    private int getUpBaseAborts;          // cap aborts, this run (persistent)
    private bool getUpBaseFast;           // the fast-path fired this attempt
    // -poseGetUpBaseRise 1: hold the press down over the whole fast-path Rise, not only the gate holds;
    // the stroke lifts the body off its base even while both feet read grounded.
    private bool getUpBaseRise;           // the whole-rise press hold, asked
    // -poseGetUpBaseSettle S > 0: the fast path needs the base held S s with |pelvVelY| <= SettleVel,
    // so it cannot fire off a mid-impact contact (the drives would turn the impact into a bounce).
    private bool getUpBaseSettleOn;
    private float getUpBaseSettleSec;      // the dwell the base must hold, s
    private float getUpBaseSettleVel = 0.6f;
    private float getUpBaseSettleT;        // the dwell so far, this attempt
    private int getUpBaseSettleBlocks;     // would-be fires refused, this run
    private bool getUpBaseSettleWarned;    // one log line per refusal episode
    // Settle-clock forms for a flickering base flag (both off by default): SettleDrop D = dropouts
    // <= D s keep the clock; SettleDuty W = the quiet share over the last W s must reach DutyFrac,
    // with this frame quiet. W > 0 selects the duty form over the dwell.
    private float getUpBaseSettleDrop;     // the debounce tolerance, s (0 = strict)
    private float getUpBaseSettleDutyWin;  // the duty window, s (0 = not the duty form)
    private float getUpBaseSettleDutyFrac = 0.6f;
    private float getUpBaseSettleGap;      // the dropout so far, s (the debounce's)
    private float getUpBaseSettleDuty;     // the duty readout, 0..1
    private const int GetUpSettleDutyCap = 512;    // 512 x 5 ms = 2.56 s of ring
    private readonly float[] getUpSettleDutyRingT = new float[GetUpSettleDutyCap];
    private readonly float[] getUpSettleDutyRingQ = new float[GetUpSettleDutyCap];
    private int getUpSettleDutyHead;
    private int getUpSettleDutyCount;
    private float getUpSettleDutySum;      // quiet seconds inside the window
    private float getUpSettleDutyAge;      // wall seconds since the attempt began
    // -poseGetUpBaseLoad F > 0: the fast path and the Tuck exit also need both soles >= F x weight,
    // held LoadSec (dropouts <= LoadDrop tolerated) - a pose-flag base can carry almost no load.
    private float getUpBaseLoadFrac;       // the per-foot load floor, x weight (0 = off)
    private float getUpBaseLoadSec = 0.1f; // the load must hold this long, s
    private float getUpBaseLoadDrop;       // the dropout tolerance, s (0 = strict)
    private float getUpBaseLoadT;          // the hold so far, this attempt
    private float getUpBaseLoadGap;        // the dropout so far, s
    private int getUpBaseLoadBlocks;       // load-refused fires, this run
    private bool getUpBaseLoadWarned;      // one log line per refusal episode
    // -poseGetUpLoadExt 1 (Tuck): while both soles carry >= Keep x weight the wait's re-fold is
    // skipped; the Rise fires once their sum >= Fire x weight for Sec s (dropouts <= Drop). 0 = off.
    // LoadExtRelatch: re-latch the leg pose at that fire, so the Rise starts from the fold.
    private bool getUpLoadExtRelatch;
    private bool getUpLoadExtOn;
    private float getUpLoadExtKeep = 0.04f;  // the per-foot keep floor, x weight
    private float getUpLoadExtFire = 0.10f;  // the sum fire floor, x weight
    private float getUpLoadExtSec = 0.3f;    // the sum must hold this long, s
    private float getUpLoadExtDrop = 0.05f;  // the dropout tolerance, s
    private float getUpLoadExtT;             // the fire clock, s
    private float getUpLoadExtGap;           // the dropout so far, s
    private int getUpLoadExtWindows;         // windows opened, this run
    private int getUpLoadExtFires;           // fires landed, this run
    private bool getUpLoadExtLive;           // the keep window's own state
    // -poseGetUpPushTrace 1 logs the blend keys and leg/trunk targets each frame for PushTraceSec s
    // after a fire. StandAnkleFast sets the ankle's blend key: 0 = kKnee, 1 = kk, 2 = 2*kk.
    private bool getUpPushTraceOn;
    private float getUpPushTraceSec = 2.5f;
    private float getUpPushTraceT0 = -1f;
    private int getUpStandAnkleFast;
    private float gusKeyk, gusKeyKnee, gusKeyAnkle;            // the blend's keys
    private float gusTgtHipL, gusTgtKneeL, gusTgtAnkleL;       // the left leg's targets
    private float getUpBaseSquatDeg;       // the sit-back depth, deg (0 = off)
    private float getUpStandHipFirst;      // the knee's delay, blend units (0 = off)
    private float getUpStandKneeClimb;     // the knee's climb gate, m (0 = off)
    // -autoGetUp D: a keyless down held D s counts as the walk key held, so the body tries to rise
    // on its own (knees -> arms -> sit -> stand). 0 = off: no walk key, no rise.
    private float getUpAutoDwell;
    private float getUpAutoAge;
    private bool getUpAutoKey;
    // Per-joint extension release offsets, blend units: kJ = (kk - SJ) / (1 - SJ), so a joint can lag;
    // a hip that reaches the stand pose before the knees unfold pitches the body forward.
    private float getUpStandHipStart;          // the hip's release offset (0 = kk)
    private float getUpStandKneeStart = -1f;   // the knee's offset (<0 = hipFirst rule)
    private float getUpStandAnkleStart = -1f;  // the ankle's offset (<0 = kKnee)
    // Move guard (-poseGetUpMoveGuard 1): the Rise clock holds while the deck moves (MoveGuardMax cap).
    // Trunk governor (-poseGetUpTrunkGov 1 gate, 2 counter-lean, 3 both): bounds the torso pitch.
    // Both off by default; the semantics live in the Rise stage.
    private bool getUpMoveGuardOn;
    private float getUpMoveGuardMax = 3f;
    private float getUpMoveGuardHeld;
    private int getUpMoveGuardHolds;
    private bool getUpMoveGuardInHold;
    private float getUpTrunkGovMode;
    private float getUpTrunkGovDeg = 40f;
    private float getUpTrunkGovK = 0.5f;
    private float getUpTrunkGovMax = 2f;
    private float getUpTrunkGovHeld;
    private int getUpTrunkGovHolds;
    private bool getUpTrunkGovInHold;
    // -poseGetUpSitStand 1: a quiet upright sit, both soles loaded and parked low (IsDown cannot see
    // it), held Hold s fires a Rise attempt from the pose as it sits; while the window holds, the
    // bound's clock is deferred up to Max s. Off by default.
    private bool getUpSitStandOn;
    private float getUpSitStandHold = 0.10f;
    private float getUpSitStandMax = 6f;
    private float getUpSitStandKeep = 0.05f;
    private float getUpSitStandVel = 0.50f;
    private float getUpSitStandDropMin = 0.15f;
    // The sit window's drop ceiling, m (-poseGetUpSitStandDropMax). 0.50 fits the deck's high sit; a
    // flat floor-sit parks ~0.9 m below stand and needs more. Moves the window only, not IsDown.
    private float getUpSitStandDropMax = 0.50f;
    private float getUpSitStandT;          // the quiet dwell (Tick-side clock)
    private float getUpSitStandExt;        // the live sit attempt's ext seconds
    private int getUpSitStandFires;        // fires (report)
    private bool getUpSitActive;           // the live attempt is a sit-fired one
    private bool getUpSitWinNow;           // the window, as of this tick (trace)
    // -poseGetUpSitPullKnee K > 0: a sit-fired attempt first drives the legs to the latch + (knee +K,
    // hip -H, ankle -A) over PullSec s, folding the heels under the body (the stand blend alone never
    // folds them), then extends from that pulled pose to the stand targets.
    private float getUpSitPullKnee;        // the pull's knee bulge, deg (the enabler)
    private float getUpSitPullHip;         // the thigh's swing from the latch, deg
    private float getUpSitPullAnkle;       // the ankle's release from the latch, deg
    private float getUpSitPullSec = 0.7f;  // the pull phase's own duration, s
    private bool getUpSitPullOn;           // knee > 0 at parse
    private float getUpSitPullKey;         // pull share 0..1, then 1 + ext share (trace)
    private bool getUpSitPullLogged;       // one enter line per attempt
    // Seat mode: 1 arms-first (press until the palms carry, then a pull gated on sole or palm load);
    // 2 fold-only (the pull advances only while the soles carry); 3 = 1 plus the press handing over
    // to the soles over the extension's first Hand s. The pull clock freezes while gated. 0 = off.
    private float getUpSitSeatMode;        // 0 off, 1 arms-first, 2 fold-only, 3 arms+handover
    private float getUpSitSeatPress = 0.9f;    // the seat press level, 0..1
    private float getUpSitSeatPressMin = 0.10f;// the palms' engaged load, BW each
    private float getUpSitSeatPressMax = 1.2f; // the press phase's cap, s
    private float getUpSitSeatKeep = 0.05f;    // the soles' keep load, fraction of W (summed)
    private float getUpSitSeatGrace = 0.15f;   // a gate dropout up to this is not a close, s
    private float getUpSitSeatHand = 0.8f;     // mode 3: the handover ramp, s
    private float getUpSitSeatFrozenMax = 2.5f;// the freeze budget, s (bound deferral cap)
    private float seatPressUsed;           // the press phase's spent seconds
    private float seatPullT;               // the gated pull clock, s
    private float seatExtT;                // the extension clock, s
    private float seatFrozenT;             // the pull's frozen seconds, s
    private int seatGatePasses;            // gate openings (report)
    private int seatPhase = -1;            // -1 idle, 0 press, 1 pull, 2 ext
    private float seatPressNow;            // the ask level this frame (-1 = historic)
    private float seatGateLastOk = -999f;  // the last frame the gate read open
    private bool seatGateNow;              // the gate as of this frame (trace)
    private bool seatGatePrev;             // the previous frame's gate (edge count)
    // Drag guards (0 = off): DragGap G ends the drag once both feet are within G m of the CoM
    // (comX - footX <= G) and extends from that exit pose; DragTip T freezes the pull clock while the
    // body tilt deviates > T deg from the drag's start pitch, so the fold can't feed its own runaway.
    private float getUpSitDragGap;         // the gap exit, m (0 off)
    private float getUpSitDragUnder;       // CYCLE 106: -poseGetUpSitDragUnder: the drag's
                                           // two-sided gap exit, m (0 = the one-sided rule)
    private float getUpSitDragTip;         // the tip freeze band, deg (0 off)
    private float getUpSitDragTilt = 999f; // the drag's start pitch (the tip datum)
    // Seated heel-draw: drawing the heels under a seated torso throws the body backward, so (armed)
    // the trunk leans forward through the draw, scaled by the pull's progress. 0 = off.
    private float getUpSitDrawLean;        // -poseGetUpSitDrawLean: the counter-lean, deg
    private float getUpSitDrawSeat = 40f;  // -poseGetUpSitDrawSeat: the draw-start pitch gate, deg
    private bool getUpSitDrawLogged;       // one hold line per attempt
    // Heel plant (scale 0 = off): while the pull's gate waits for sole load, drive the feet down and
    // close instead of idling; the frozen budget is suspended for up to PlantMax s, and the draw
    // resumes the moment the sole load returns.
    private float getUpSitDrawPlant;       // -poseGetUpSitDrawPlant: master scale (0 = off)
    private float getUpSitDrawPlantKnee = 45f;   // the knee's fold delta (the close axis), deg
    private float getUpSitDrawPlantHip = -25f;   // the hip's delta (signed: - flex, + extend), deg
    private float getUpSitDrawPlantAnkle = -12f; // the ankle's delta (signed), deg
    private float getUpSitDrawPlantSec = 1.0f;   // the plant's ramp, s
    private float getUpSitDrawPlantMax = 2.0f;   // the plant's window before the count resumes, s
    private float seatDrawPlantT;          // the wait's own clock, s (the frozen-budget shield)
    private float seatDrawPlantK;          // the plant's key (0..1; the ramp), trace
    private bool seatDrawPlantLogged;      // one enter line per attempt
    private bool seatDrawPlantReleased;    // one release line per attempt
    // Plant re-arm (0 = off): every re-closed gate starts a fresh plant episode, up to the episode cap,
    // so the draw advances in supported steps. The release stays gated on the sole load.
    private float getUpSitDrawPlantRe;     // -poseGetUpSitDrawPlantRe: per-episode re-arm (0 = off)
    private float getUpSitDrawPlantEps = 3f; // -poseGetUpSitDrawPlantEps: episode cap (incl. the first)
    private float seatDrawPlantEpT;        // the episode's own window clock, s (CYCLE 114)
    private int seatDrawPlantEps;          // episodes entered since the fire (trace)
    private bool seatDrawPlantPrevGate;    // the gate's previous frame (the re-arm edge)
    private float seatGapL, seatGapR;      // comX - footX, this frame (trace)
    private float seatBodyTilt;            // the body tilt, this frame (tip source)
    private float seatTipDev;              // |body tilt - drag start tilt|, deg (trace)
    private float seatPullExitKey = 1f;    // the drag's exit share (the ext's from-pose)
    // -poseGetUpSitReseat 1: between the drag's exit and the extension the legs take turns adding
    // per-leg budget (Knee folds the heel in, Hip slides the body, Ankle releases the toe). Ends on the
    // corridor (gaps <= ReseatGap, soles >= ReseatLoad*W for LoadSec), spent shares or ReseatMax.
    private float getUpSitReseat;          // -poseGetUpSitReseat: 1 on, 0 off
    private bool getUpSitReseatOn;
    private float getUpSitReseatGap = 0.18f;
    private float getUpSitReseatKnee = 70f;
    private float getUpSitReseatHip;
    private float getUpSitReseatAnkle;
    private float getUpSitReseatSec = 0.60f;
    private float getUpSitReseatMax = 4.0f;
    private float getUpSitReseatLoad = 0.05f;
    private float getUpSitReseatLoadSec = 0.20f;
    private float seatResL, seatResR;      // per-leg shares 0..1 (trace)
    private float seatReseatT;             // the stage's clock, s (trace)
    private float seatResTurnT;            // the current turn's clock, s
    private float seatResLoadT;            // the sustained-sole-load clock, s
    private int seatResTurn;               // the active leg (0 L, 1 R)
    private bool seatReseatDone;           // the hand-off happened
    private bool seatResLogged;            // one enter line per attempt
    private float seatResHipL, seatResKneeL, seatResAnkleL;   // the hand-off pose
    private float seatResHipR, seatResKneeR, seatResAnkleR;   // (the ext's from)
    // -poseGetUpSitStep 1: fold ONE leg at a time while the other stays planted (folding both lifts
    // both soles and the body dives off its seat). Shares ramp L then R per round; ends on the corridor
    // (gaps <= StepGap, load held StepLoadSec), the rounds or StepMax. Hand-off pose goes to seatRes*.
    private float getUpSitStep;            // -poseGetUpSitStep: 1 on, 0 off
    private bool getUpSitStepOn;
    private float getUpSitStepHip = 60f;   // per-leg budget: hip delta from the latch
    private float getUpSitStepKnee = 160f; // per-leg budget: knee delta
    private float getUpSitStepAnkle = 50f; // per-leg budget: ankle delta
    private float getUpSitStepSec = 0.55f; // per-leg travel ramp, s
    private float getUpSitStepHold = 0.30f;// per-leg hold at the target, s
    private float getUpSitStepPlant;       // ease back by this share during the hold (0 = keep)
    private float getUpSitStepGap = 0.15f; // the corridor gap, m
    private float getUpSitStepLoad = 0.05f;// the corridor load, fraction of W (summed)
    private float getUpSitStepLoadSec = 0.20f; // the sustained-load clock, s
    private float getUpSitStepMax = 6.0f;  // the whole machine's budget, s
    private float getUpSitStepRounds = 1f; // full L+R rounds (each round adds a budget pass)
    private float getUpSitUnder;           // CYCLE 105: -poseGetUpSitUnder: the two-sided
                                           // under-gate + the full-delta march (0 = off)
    private float stepT;                   // the machine's clock, s
    private float stepFracL, stepFracR;    // the per-leg shares (published to the trace)
    private float stepFromFrac;            // the active leg's share at its phase's start
    private int stepLeg;                   // the active leg (0 L, 1 R)
    private int stepPhase;                 // 0 travel, 1 hold
    private float stepPhaseT;              // the phase's clock, s
    private int stepRound;                 // 0-based round index
    private bool stepDone;                 // the hand-off happened
    private bool stepLogged;               // one enter line per attempt
    private float stepLoadT;               // the sustained-sole-load clock, s
    // -poseGetUpSitExtArrest 1: the seat extension's clock freezes while the torso pitch rate exceeds
    // ArrestRate deg/s or the pelvis sits ArrestDrop m below its ext-start height; capped at
    // ArrestMax s, after which the clock resumes ungated. Off by default.
    private float getUpSitExtArrest;          // -poseGetUpSitExtArrest: 1 on, 0 off
    private bool getUpSitExtArrestOn;
    private float getUpSitExtArrestRate = 40f;   // the torso pitch rate bound, deg/s
    private float getUpSitExtArrestDrop = 0.10f; // the pelvis drop bound below ext start, m
    private float getUpSitExtArrestMax = 2.5f;   // the freeze budget, s
    private bool seatExtArrestInit;              // the ext-start datum captured
    private float seatExtPelvY0;                 // the ext-start pelvis Y, m
    private float seatExtPrevTilt;               // the previous frame's body tilt, deg
    private float seatExtArrestT;                // the frozen clock, s
    private int seatExtArrestTrips;              // trips this attempt
    private bool seatExtArrestBad;               // the live state
    // -poseGetUpSitShift 1: the step exits with unloaded soles, so the legs drive partway back out of
    // the fold (hip extension, knee release, plantar press) seeking sole load; exits on sum Fz >=
    // Load*W held LoadSec, or the stage budget. Off by default.
    private float getUpSitShift;              // -poseGetUpSitShift: 1 on, 0 off
    private bool getUpSitShiftOn;
    private float getUpSitShiftHip = 25f;     // the hip extension delta from the capture, deg
    private float getUpSitShiftKnee = 45f;    // the knee release delta (toward straight), deg
    private float getUpSitShiftAnkle = 15f;   // the ankle plantar press delta, deg
    private float getUpSitShiftLean;          // the trunk lean over the stage, deg (0 = historic)
    private float getUpSitShiftSec = 2f;      // the stage's budget, s
    private float getUpSitShiftRamp = 0.9f;   // the deltas' travel, s
    private float getUpSitShiftLoad = 0.15f;  // the exit load, fraction of W (Fz sum)
    private float getUpSitShiftLoadSec = 0.25f; // the sustained-load clock, s
    private float getUpSitShiftMax = 4f;      // the rise-bound deferral budget, s
    private float sitShiftT;                  // the stage's clock, s
    private bool sitShiftDone;                // the hand-off happened
    private bool sitShiftEntered;             // one enter line per attempt
    private float sitShiftLoadT;              // the sustained-sole-load clock, s
    private float sitShiftFzMax;              // the stage's peak sole load, N
    private float sitShiftGapMin = 999f;      // the stage's smallest foot-CoM gap, m
    private float sitShiftFrac;               // the deltas' fraction
    // ─── sit plant stage ───
    // -poseGetUpSitPlant 1: after the step hand-off, unfold knee/hip/ankle from the captured fold to
    // reach the deck; the extension waits for sole contact (or the reach budget). Off by default.
    private float getUpSitPlant;              // -poseGetUpSitPlant: 1 on, 0 off
    private bool getUpSitPlantOn;
    private float getUpSitPlantKnee = 110f;   // the knee's unfold delta, deg
    private float getUpSitPlantHip = 25f;     // the hip's extension delta, deg
    private float getUpSitPlantAnkle = 25f;   // the ankle's plantar delta, deg
    private float getUpSitPlantSec = 1.2f;    // the reach's travel ramp, s
    private float getUpSitPlantFz = 0.05f;    // the contact threshold, fraction of W (summed)
    private float getUpSitPlantFzSec = 0.10f; // the contact must hold this long, s
    private float getUpSitPlantMax = 2.0f;    // the reach's budget, s
    private float sitPlantT;                  // the stage's clock, s
    private bool sitPlantDone;                // the hand-off happened
    private bool sitPlantEntered;             // one enter line per attempt
    private float sitPlantFrac;               // the deltas' fraction
    private float sitPlantContactT;           // the contact clock, s
    private float sitPlantFzMax;              // the stage's peak sole load, N
    // Continuous arc: the extension runs on its own clock, held while the torso pitch rate exceeds
    // ArcRate, and the torso ask is tied to the pelvis height. ArcSec 0 = off.
    private float getUpSitArcSec;          // -poseGetUpSitArc: full arc duration, s (0 off)
    private float getUpSitArcTorso = 40f;  // -poseGetUpSitArcTorso: base torso allowance, deg
    private float getUpSitArcRate = 40f;   // -poseGetUpSitArcRate: pitch-rate gate, deg/s (0 off)
    // When the arc's height tie is live: 0 = from the attempt's first frame; 1 = only once the pull
    // completes (the press and pull keep their trunk asks, so the palms can load); 2 = never.
    private float getUpSitArcTieExt;
    private float getUpSitArcClock;        // the arc's own clock, s (rate-gated)
    private float getUpSitArcHeld;         // frozen time, s (the trace)
    private int getUpSitArcHolds;          // hold episodes, this attempt
    private bool getUpSitArcInHold;
    private float getUpSitArcPrevTilt;     // the rate's datum
    private float getUpSitArcTiltRate;     // the EMA rate, deg/s (the trace)
    private float getUpSitArcHShare;       // the height share (the trace)
    private float getUpSitRockSec;         // CYCLE 106: -poseGetUpSitRock: the torso's own
                                           // rock, s (0 = the height tie alone)
    private float getUpSitRockGate;        // CYCLE 106: -poseGetUpSitRockGate: hold the ext
                                           // clock until the rock completes (0 = off)
    private float getUpSitRockClock;       // the rock's own clock, s (reset at the fire)
    // Co-over gate (-poseGetUpSitExtOver G, m; 0 = off): the extension clocks freeze while a foot is
    // more than G ahead of the CoM, holding the fold while the torso rocks the body over the feet.
    // A body that never gets over ends on the attempt's own rise bound.
    private float getUpSitExtOver;         // -poseGetUpSitExtOver: CoM-over-feet band, m
    private float getUpSitArcBasePelvY = -999f;  // the fire's pelvis datum
    private float getUpTracePelvVel;       // the trace's pelvisVelY witness
    private float getUpTrunkGovBias;
    private float getUpTrunkGovCap;        // ROUND 42b: the counter-lean cap, deg (0 = uncapped)
    private float getUpRiseStartPelvisY = -999f;   // the Rise's own pelvis datum
    private float rigWeightCacheN;         // the human's weight, N (cached)
    // Hip-channel debug log window, s (To <= From = off).
    private float hipDbgFrom;
    private float hipDbgTo;
    // Get-up hip owner: > 0.5 = the leg drive alone writes the hips; 0 = both writers.
    private float getUpHipOwner;
    // Leg drive and pelvis law write the same hip muscles; with opposite signs they cancel. HipArb 1:
    // skip a side's pelvis-law write when this frame's leg demand is anti-phase (both |signal| >=
    // HipArbBand), so the leg demand lands; 2 = only in the Rise stage. 0 = off.
    private float getUpHipArb;             // 0 off; 1 sign-aware; 2 sign-aware, Rise only
    private float getUpHipArbBand = 0.1f;  // the anti-phase deadband, |signal|
    // A sit attempt's own arb band: in the quiet sit the leg demand sits right at the regular band's
    // edge, so the arb would never engage there. < 0 = use the regular band.
    private float getUpSitStandArbBand = -1f;
    private float legHipSigL, legHipSigR;  // the leg drive's own hip demand, this frame
    private float legHipSigT = -1f;        // the frame stamp of that capture
    private int getUpArbSuppL, getUpArbSuppR;        // suppressed writes, this attempt
    private int getUpArbSuppLTot, getUpArbSuppRTot;  // suppressed writes, this take
    private bool getUpArbFirstL, getUpArbFirstR;     // one log line per side, per attempt

    // ─── get-up hip owner ───
    // Leg drive and pelvis law step the hip muscles equally with opposite signs, so neither lands.
    // getUpHipOwner > 0.5 gives the hips to the get-up leg drive alone; 0 = both write.
    private void DriveHipsGetUp(float pelvisTilt, float pelvisAngVel)
    {
        if (getUpHipOwner > 0.5f) return;   // the get-up leg drive owns the hips
        if (getUpHipArb > 0.5f)
        {
            DriveHipsArb(pelvisTilt, pelvisAngVel);
            return;
        }
        DriveHips(pelvisTilt, pelvisAngVel, 0f, true, true, 0f, false);
    }

    // DriveHips' arithmetic (target tilt 0, no hold mix), except a side whose leg-drive demand this
    // frame is anti-phase with the pelvis signal is not written, so the leg drive's demand lands.
    private void DriveHipsArb(float pelvisTilt, float pelvisAngVel)
    {
        bool live = legHipSigT == Time.fixedTime;
        if (getUpHipArb > 1.5f && getUpStage != GetUpStage.Rise) live = false;
        float error = Mathf.DeltaAngle(pelvisTilt, 0f)
            / Mathf.Max(1f, balance.pelvisErrorReferenceDegrees);
        float speed = pelvisAngVel / Mathf.Max(1f, balance.speedReferenceDegPerSec);
        float signal = Mathf.Clamp(
            balance.pelvisPGain * error - balance.pelvisDGain * speed, -1f, 1f);
        HipDbgWrite("pelvL", signal, 0f, leftHipJoint, balance.leftHipFlexor, balance.leftHipExtensor);
        HipDbgWrite("pelvR", signal, 0f, rightHipJoint, balance.rightHipFlexor, balance.rightHipExtensor);
        bool supL = live && HipArbAntiPhase(legHipSigL, signal);
        bool supR = live && HipArbAntiPhase(legHipSigR, signal);
        if (supL)
        {
            getUpArbSuppL++;
            getUpArbSuppLTot++;
            if (!getUpArbFirstL)
            {
                getUpArbFirstL = true;
                Debug.Log("GETUP-HIPARB t=" + N(Time.fixedTime, 3) + " side=L legSig="
                    + N(legHipSigL, 2) + " pelv=" + N(signal, 2) + " stage=" + getUpStage);
            }
        }
        if (supR)
        {
            getUpArbSuppR++;
            getUpArbSuppRTot++;
            if (!getUpArbFirstR)
            {
                getUpArbFirstR = true;
                Debug.Log("GETUP-HIPARB t=" + N(Time.fixedTime, 3) + " side=R legSig="
                    + N(legHipSigR, 2) + " pelv=" + N(signal, 2) + " stage=" + getUpStage);
            }
        }
        if (!supL) ApplyHipSignal(true, signal);
        if (!supR) ApplyHipSignal(false, signal);
    }

    // Anti-phase = opposite signs, both magnitudes at or above the band.
    private bool HipArbAntiPhase(float legSig, float pelvSig)
    {
        float band = getUpHipArbBand;
        // A live sit attempt uses its own band (its cancellation sits exactly at the regular edge).
        if (getUpSitStandOn && getUpSitActive && getUpSitStandArbBand >= 0f)
            band = Mathf.Clamp(getUpSitStandArbBand, 0f, 1f);
        if (Mathf.Abs(legSig) < band) return false;
        if (Mathf.Abs(pelvSig) < band) return false;
        return (legSig > 0f) != (pelvSig > 0f);
    }
    private float getUpBackSince = -1f;
    private int getUpRollBounds;
    private bool getUpRollBlocked;
    // The settle pose, latched when the get-up starts: the tuck is a delta from the pose the body
    // landed in, so the same shape holds on every build.
    private float getUpHipL, getUpHipR, getUpKneeL, getUpKneeR, getUpAnkleL, getUpAnkleR;
    private float getUpTrunkStartDeg;
    // The grip, per side: the held state, the press clock, the anchored slip.
    private readonly Collider2D[] handCollider = new Collider2D[2];
    private readonly bool[] gripHeld = new bool[2];
    private readonly float[] gripUnloadAge = new float[2];
    private readonly Vector2[] gripAnchor = new Vector2[2];
    private readonly float[] gripSlipM = new float[2];
    private readonly float[] gripLoad = new float[2];      // body weights
    private readonly float[] gripFnN = new float[2];
    private readonly float[] gripTfN = new float[2];
    private readonly float[] gripCapN = new float[2];
    private readonly int[] gripEngages = new int[2];
    private readonly int[] gripTears = new int[2];
    private readonly bool[] gripTornThisStep = new bool[2];
    // Press latch: a live press ask (ArmDrive's press request or a running stroke) over a real touch
    // also engages the grip - the palm's measured load alone stays too low on flat ground and the
    // palms skate. -poseHandGripPress 0 = load-only engage.
    private bool handGripPressOn = true;           // -poseHandGripPress (default ON)
    private readonly bool[] gripPressWants = new bool[2];
    private readonly bool[] gripPressLogged = new bool[2];
    private ContactFilter2D handGripFilter;
    private bool handGripFilterReady;
    private readonly ContactPoint2D[] handGripContacts = new ContactPoint2D[12];
    // The get-up trace (<label>-getup.csv): palm loads, grip material and engage/hold/release, stage
    // and the stand bars with frame numbers. A separate file so the walk CSV's columns stay unchanged.
    private System.IO.StreamWriter getUpTrace;
    private readonly System.Text.StringBuilder getUpTraceRow = new System.Text.StringBuilder(320);
    private float getUpTraceFlushAge;
}
