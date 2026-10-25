using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

// Run one human without the editor window, for numerical balance analysis.
// Activated by the -trial argument on the built player's command line:
//   BalanceTrial.exe -batchmode -nographics -trial -duration 10 -label base -logFile -
//   -fixedDelta 0.005 — physics step (0.02 = 50 Hz, 0.005 = 200 Hz)
//   -activationSpeed 10 — muscle force rise speed
//   -pushImpulse 20 -pushTime 5 — torso push (N·s, + forward) at second 5
//   -crouch 1 -crouchTime 5 -crouchHold 10 — crouch target via MotionIntent
//   -squatRise 1 -squatRiseTime 5 -squatRiseHold 1 — A4 scripted
//     squat-and-rise: one crouch-and-extend cycle while standing through the
//     same MotionIntent.crouch channel as -crouch. The rise is the controller's
//     own crouchReleaseRatePerSecond ramp (~1.1 s for a full 1 -> 0), and the
//     output channel is fzTotal + pelvisVelY/pelvisAccY in the trial CSV.
//   -lean 1|-1 -leanTime 5 -leanHold 10 — trunk lean (+ forward, − back)
//   -punch 1 -punchTime 5 -punchHold 8 — arm strike (the punch) via
//     MotionIntent.punch, the same field the editor's F key writes. The trial
//     writes the REQUEST only; the stroke (wind-up -> extension -> return on
//     shoulder/elbow/wrist) and its authority are ArmDrive's (-punchWind,
//     -punchExtend, -punchReturn, -punchInterval, -punchShoulderHit,
//     -punchElbowHit, -punchWristHit, -punchSide, -punchTorque, ...).
//     -punchPause <s> (punch cycle p3): the hold window fires twice, that many
//     seconds between the bursts (0 = one window, the landed shape).
//     -punchBursts "9,9,9" (punch cycle p4): the burst schedule by length -
//     that many windows of those seconds, punchPause apart (empty = the
//     landed shapes: one window, or the p3 pair when punchPause > 0).
//   -kick 1 -kickTime 5 -kickHold 8 — leg strike (the kick) via
//     MotionIntent.kick, the same field the editor's G key writes. The trial
//     writes the REQUEST only; the stroke (lift -> chamber -> extension ->
//     return on the kicking leg's hip/knee/ankle) and its authority are
//     LegStrikeDrive's (-kickLift, -kickChamber, -kickExtend, -kickReturn,
//     -kickInterval, -kickSide, -kickHipLift, -kickKneeChamber, -kickKneeHit,
//     -kickAnkleHit, -kickTorque, ...).
//   -standLeg left|right|0 -standLegTime 5 -standLegHold 10 — one-leg stance
//   -march 1 -marchTime 5 -marchDuration 24 -marchHold 2.5 -marchPlant 1.5
//     -marchFirst left|right — in-place clock on standLeg (not walk / WalkLift)
//   -marchHip / -marchKnee — air pose while marching (deg; hip minus, knee plus)
//   -marchStanceP / -marchAnkleCap / -marchAnkleToe / -marchAnkleHeel
//     / -marchComDelay / -marchPoseHip / -marchPoseKnee
//     — vertical pelvis; ankle cap after true air; toe/heel after pose-up
//   -dog 1 — run a dog instead of a human (humanCount = 0, flat ground)
//   -arena 1 — ARENA-1 parameter sweep: ground only, one human every 500 ms at one
//     spawn point, genomes of the pose-drive walk parameters, distance at 15 s.
//     -arenaSeed 1 (genome stream), -arenaSeconds 90 (sim length),
//     -arenaInterval 0.5 (s between spawns; a large value is the solo control - the
//     same genome with no neighbours at all),
//     -arenaWallLimit 3000 (s of wall time before the run is cut short),
//     -arenaNumber 0.34 (digit height on the head, m), -arenaCamera 5 (wide lens),
//     -arenaPhoto 2.2 -arenaPhotoTime 12 (zoomed photo frames), -arenaGroundMeters 200
//     -arenaCamFollow 1 -arenaLead 2.5 — ARENA CYCLE 59: the leading edge of the take
//     rides with the furthest alive body (ArenaRunner.cameraFollowLeader); 0 = the
//     fixed-lens control take.
//   -dog 1 -bird 1 — both bodies in one scene (bird is not dropped)
//   -dogBird 1 — dog trial also spawns a live bird inside DogDrive.noticeRange
//   -dogBirdX 2 — bird X from the dog, m (clamped inside noticeRange; default 2)
//   -dogLeap 1 -dogLeapTime 2 -dogPreyX 1.4 — leap attack after settle, prey mark
    //   -dogWalk 1 -dogWalkTime 1 — clocked gait after settle (not human -walk)
    //   -dogRunSpeed 1.4 — bound / gallop speed, m/s (0 = old tiny diagonal)
    //   -dogPawFriction 1.2 — pad contact friction (1.2 = human sole)
    //   -dogBodyFriction 0.4 — body plating contact friction (explicit default)
//   -flower 1 — garden bed (picture only, no hinges). -flowerKind rose|…
//   -flowerCount 12 — how many plants in the row
//   -tree 1 — tree (can pair with -bird: birds on branches)
//   -treeKind oak|pine|willow|bush|poplar|apple|lilac|hazel|cedar|plum — crown recipe (default oak)
//   -treeSeed 1 — local fork Random
//   -treePerch 1 — seat birds on PlantTree.GetPerchSlots
//   -treePerchLoad 4 — injected sitter load, kg, at one perch slot (plant applies it)
//   -treePerchSlot 0 — which slot (twigs first, same order as GetPerchSlots)
//   -treePerchLoadUntil 4 — release the load at 4 s, so one run shows bend + return
//   -treeTriggers — build the landing ring (owner's sheet 25.09): isTrigger
//                   circles at the branch forks, on the branch segments and one
//                   at the trunk's top. No box collider anywhere on the tree.
//   -treeTriggerCap 16 — ring size (the sheet's sample carries 16 points)
//   -treeTriggerRadius 0.05 — trigger circle radius, m (detection only)
//   -treeTriggers also arms the ring's own open-air read (LND2, 26.09): the
//                   run rows landingSky* and <label>_sky.csv / <label>_wood.csv
//                   carry the plant's own claw-band read of every mark.
//   -treeLoadOnTrigger — the injected load targets the ring, not the BirdPerch pads
//   -treeTriggerSlot 2 — which ring point takes the load (GetLandingSlots() order)
//   -treeProbe — fly the bird's-crossing probe: the bird's body box through the
//                crown, counting solid contacts (must be 0) and trigger overlaps
//   -treeProbeSpeed 0 — weave speed override, m/s (0 = fit the whole path in 6 s)
//   -treeWind 0 — no sail (check that branches hold the pose)
//   -treeWindUntil 4 — the sail STOPS at 4 s on the oak too (T1-sway switch,
//                      19.09): the wind-off run that proves the crown decays
//                      back to rest (0 = wind for the whole trial)
//   -treeSeg 4 — the bole is N dynamic hinge segments instead of the folded
//                static trunk (0 = today's rig, byte-identical)
//   -treeSegE 0 — game modulus (MPa) for the beam law k = E*pi*r^4/(4L);
//                0 = auto-tune at bind (stability + 0.6 Hz first mode)
//   -treeLeaves 1 — one dynamic leaf cluster per twig tip plus one inner per
//                limb (no collider). Off = picture-only leaves.
//   -apple 1 — apple tree (AppleTree : TreeBase : PlantBase; sway + fruit)
//   -appleSeed 1 — local fork Random
//   -appleWind 0 — no sail (hold); default 0.12 sways the crown
//   -appleWindUntil 4 — the sail STOPS at 4 s: the wind-off run that proves the
//                       crown decays back to rest (0 = wind for the whole trial)
//   -appleHeight 2.5 — force crown height, m (overrides the seed-derived 2–5 m)
//   -bird 1 — run a bird instead of a human (human flags are not read)
//   -birdMode stand|fly|walk|glide|sit|attack -takeoffDelay 0 — stance with no takeoff
//   -birdRig ragdoll|flock — ragdoll for stance, flock for a pack (one body)
//   -birdFacing 1|-1 — nose along +X or −X (flock: picture on Look)
    //   -birdKind crow|chicken — body recipe (chicken: ground, not cruise)
    //   -birdDrive wander|prey — brain writes mode (flock: wander + -birdCount)
    //   -birdPrey 10 — live Insect on dirt ahead, m (prey drive defaults to 10; 0 — none)
    //   -birdThreat 8 — threat mark ahead, m (0 — none; cry + Fly, not Attack)
    //   -threatRange 12 — “sees threat” range, m
//   -glideLead / -sitMin / -sitMax / -flyMin / -flyMax / -flockLeash / -aiStride
//     / -homeFaceDeadzone — wander timing, flock brain stride, Glide nose deadzone at the slot
//   -birdDriveEach 1 — N× BirdDrive (per bird) instead of one BirdFlockDrive
//   -flapHz / -flapAmp / -hover — flap and mean thrust (fractions of weight)
//   -stopFlap 4 — drop the flap at second 4: default is glide and land
//   -stopFlapMode glide|stand — glide (shallow landing) or stand (stone drop)
//   -walk 1 -walkTime 1.5 -walkDuration 38.5 -walkStance 10 -walkTransfer 8 -walkFirst right
//   -useFallCatchWalk 1 -fallComLead 0.025 -fallComLeadTrigger 0.015 -plantMinDuration 0.5 -plantLeadWait 1 -reachTimeout 1 -fallAbortDeg 30
//   -catchLeadCapture 1 -catchFootLeadMin 0.035 -catchLeadMargin 0.015 (0 = former constant catchFootLead)
//   -plantCaughtLead 0.05 -plantArrestCop 0.5 — Plant roll latch and the
//   CoP-only lead arrest (0 / 0 = former lead ≤ 0 latch with the saturating
//   brake). Recover swap does not wait for the band.
    //   -fallPelvisLean 12 -fallLeanScale 0.75 -catchKneeFlex 55 -reachKneeFlex 140 -fallStanceVault 1 -fallSwingThighHold 1 -fallSwingHipLift 0.25 -fallSwingAnkleLift 0.25 -fallSwingAirAnkle 1 -fallSwingAirHeel 10 -fallSwingHeelPlant 1 -fallSwingScrapeExtend 1 -fallSwingCatchLandKnee 1 -fallSwingPlantStanceKnee 1 -fallSwingVaultRamp 1 -fallSwingCatchVaultYield 1 -fallSwingArmClock 1 -fallSwingReachLiftClock 1 -fallSwingReachArmClock 1 -fallSwingCatchArmClock 1 -fallSwingCatchExtendClock 1 -fallSwingCatchReachResume 1 -fallSwingCatchReachScrapeResume 1 -fallSwingCatchReachScrapeLiftHold 1 -fallSwingCatchReachScrapeLiftAirAge 1 -fallSwingCatchReachScrapeVaultHold 1 -fallSwingCatchReachScrapeVaultAirAge 1 -fallSwingCatchReachScrapeArmHold 1 -fallSwingCatchReachScrapeArmAirAge 1 -fallSwingCatchReachScrapeBrakeHold 1 -fallSwingCatchReachScrapeBrakeAirAge 1 -fallSwingCatchReachScrapeYieldHold 1 -fallSwingCatchReachScrapeYieldAirAge 1 -fallSwingCatchReachScrapeExtendHold 1 -fallSwingCatchReachScrapeExtendAirAge 1 -fallSwingCatchReachVaultClock 1 -fallSwingCatchReachVaultAge 1 -fallSwingCatchReachArmClock 1 -fallSwingCatchReachLiftClock 1 -fallSwingCatchReachLiftAge 1 -fallSwingCatchReachArmAge 1 -fallSwingCatchAirVaultHold 1 -fallSwingCatchAirVaultRemain 1 -fallSwingCatchAirArmHold 1 -fallSwingCatchAirExtendHold 1 -fallSwingCatchScrapeExtendHold 1 -fallSwingCatchScrapeExtendAirAge 1 -fallSwingCatchScrapeExtendAirEnterHold 1 -fallSwingCatchReachScrapeExtendEnterHold 1 -fallSwingCatchAirLiftHold 1 -fallSwingCatchScrapeLiftAirHold 1 -fallSwingCatchScrapeLiftAirEnterHold 1 -fallSwingCatchScrapeLiftAirCatchHold 1 -fallSwingCatchScrapeLiftAirCatchFold 1 -fallSwingCatchScrapeLiftAirEnterFold 1 -fallSwingCatchScrapeLiftAirEnterArm 1 -fallSwingCatchScrapeLiftAirCatchArm 1 -fallSwingCatchScrapeLiftAirCatchEndArm 1 -fallSwingCatchScrapeLiftAirCatchGroundArm 1 -fallSwingCatchScrapeLiftAirCatchAirArm 1 -fallSwingCatchScrapeLiftAirCatchAirEndArm 1 -fallSwingCatchScrapeLiftAirCatchAirGroundArm 1 -fallSwingCatchScrapeLiftAirEnterGroundArm 1 -fallSwingCatchScrapeLiftAirEnterGroundFold 1 -fallSwingCatchScrapeLiftAirCatchGroundFold 1 -fallSwingCatchScrapeLiftAirCatchAirGroundFold 1 -fallSwingCatchScrapeLiftAirCatchAirFold 1 -fallSwingCatchScrapeLiftAirCatchAirEnterFold 1 -fallSwingCatchScrapeLiftAirCatchAirEnterArm 1 -fallSwingCatchScrapeLiftAirCatchAirEnterGroundArm 1 -fallSwingCatchScrapeLiftAirCatchAirEnterGroundFold 1 -fallSwingCatchScrapeLiftAirCatchAirEnterEndArm 1 -fallSwingCatchScrapeLiftAirCatchAirEnterCatchArm 1 -fallSwingCatchScrapeLiftAirCatchAirEnterCatchFold 1 -fallSwingCatchScrapeLiftAirCatchAirEnterEndFold 1 -fallSwingCatchScrapeLiftAirCatchEndFold 1 -fallSwingCatchScrapeLiftAirCatchGroundCatchFold 1 -fallSwingCatchScrapeLiftAirCatchGroundCatchEndFold 1 -fallSwingCatchScrapeLiftAirCatchAirEndFold 1 -fallSwingCatchScrapeLiftAirCatchAirGroundCatchFold 1 -fallSwingCatchScrapeLiftAirCatchPlantExitFold 1 -fallSwingPlantStanceExtendClock 1 -fallSwingVaultFromLeftover 1 -fallSwingFallAnklePush 1 -fallSwingFallHipLiftCap 1 -fallSwingFallAnkleLift 1 -fallSwingFallHipLift 1 -fallSwingReachAirHold 1 -fallSwingReachHipLiftCap 1 -fallSwingReachHipLift 1 -fallSwingReachAnkleLift 1 -fallSwingReachHipLiftCapHold 1 -fallSwingFallHipLiftCapHold 1 -fallSwingFallVaultLead 1 -fallSwingFallLiftLead 1 -fallSwingFallFoldLead 1 -fallSwingCatchAirHipLiftCap 1 -fallSwingCatchAirHipLiftCapHold 1 -fallSwingCatchAirHipLift 1 -fallSwingCatchAirAnkleLift 1 -fallSwingCatchScrapeHipLiftCap 1 -fallSwingCatchScrapeHipLiftCapHold 1 -fallSwingCatchScrapeHipLift 1 -fallSwingCatchScrapeAnkleLift 1 -fallSwingCatchScrapeLiftLead 1 -fallSwingCatchScrapeHipLiftCapWide 1 -fallSwingCatchScrapeHipLiftCapCeil 1 -fallSwingCatchReachScrapeHipLiftCap 1 -fallSwingCatchReachScrapeHipLiftCapHold 1 -fallSwingCatchReachScrapeHipLift 1 -fallSwingCatchReachScrapeAnkleLift 1 -fallSwingCatchReachScrapeHipLiftCapWide 1 -fallSwingCatchReachScrapeHipLiftCapCeil 1 -fallSwingCatchAirHold 1 -fallSwingCatchAirEnterHold 1 -fallSwingPlantAirHold 1 -fallSwingReachAirSwingCeil 1 -fallSwingHipairCeil 1 -fallSwingRecoverHipairClear 1 — WALK-2 FSM
//   Play/WebGL uses StepPhaseDriver field defaults (useFallCatchWalk 1,
//   reachTimeout 1, fallAbortDeg 30, reachKneeFlex 140). The stand still
//   defaults -useFallCatchWalk 0 so a quiet -walk 1 stays the legacy path.
//   -run 1 -runTime 5 -runHold 20 — run (shorter Stance), with -walk or alone
//   -swingHip / -swingKnee — swing flexion (hip minus, knee plus)
//   -swingHipUnload — swing flexor-bias while grounded (signal fraction)
//   -pelvisP / -pelvisD / -pelvisRef — pelvis PD to the world vertical
//   -crouchPelvis — target pelvis tilt forward at crouch=1
//   -neckFriction / -shoulderFriction / -elbowFriction / -wristFriction —
//   absolute K of passive viscosity before BuildHuman (head shares neckFriction)
//   -neckMuscle — neck and head muscle torque, absolute, after -muscle
   //   -neckP / -neckD / -neckTilt / -neckRef — neck and head PD to the world vertical
   //   -shoulderP / -elbowP / -wristP and paired D/Base/Ref — arm joint pose
   //   -armShoulderBal / -armElbowBal / -armWristBal — CoM counter-reach (deg/unit)
   // All balance coefficients can be overridden by arguments without rebuilding the player,
// so one build serves any parameter sweep.
public class HeadlessTrial : MonoBehaviour
{
    // GameProcess checks this flag and does not build its own 50 humans,
    // or the run would drown in 700 Rigidbody2D.
    public static bool Active { get; private set; }

    // -visual without -nographics: CameraFollow.LateUpdate may run, and
    // HumanSegment uses collider-sized sprites instead of 1 m placeholders.
    public static bool VisualCapture { get; private set; }

    // ART-H4c (art lane): the rig's debug overlay - the dark joint markers
    // and the skeleton bars (HumanSegment / HumanSkeletonLines). Drawn in
    // every visual capture and in play; a look capture passes
    // -rigOverlay 0 to keep them out of the frames. Picture only: no
    // Rigidbody2D, no collider, no physics reads it.
    public static bool RigOverlay { get; private set; } = true;

    // -skinJoints 1: the Route-A joint patch skin rides the dressed look (ART-H5 R2).
    // Picture only and default off; no physics row and no other flag reads it.
    public static bool SkinJoints { get; private set; } = false;

    // -coverJoints 0|1 (ART-H5f R3): the joint covers (knee/elbow/hip) ride the same
    // -foreshorten gate; this is the round's own A/B lever - default 1 draws them, 0 is
    // the same player without the cover nodes.  Picture only; no physics row reads it,
    // and with -foreshorten 0 nothing is built either way.
    public static bool CoverJoints { get; private set; } = true;

    // -foreshorten <0..0.4> (ART-H5f): the thigh's / upper arm's picture shrinks along
    // its bone axis and the neighbour grows to cover the joint (HumanArt). Picture only,
    // default 0 = today; the walk lane's rows must not move with it.
    public static float Foreshorten { get; private set; } = 0f;

    // -fsMass hold|project (WALK CYCLE 66): the mass flavour of the shortened thigh /
    // upper arm. "project" = the segment went out of the plane, so its in-plane mass is
    // the projection (m' = m·k); "hold" = the mass is held constant. Both are probeable
    // and the round's numbers pick; default "hold" is the value the physics had before
    // the channel existed, so an ON-run without the switch carries it.
    public static bool ForeshortenMassProjected { get; private set; } = false;

    // -fsRampAt <s> + -fsRamp <v per second> (WALK CYCLE 66): THE CHANNEL'S RATE LIMIT
    // for a length that moves AFTER the build. Absent (RampAt < 0) = the value is
    // applied before the body is built (born short — the flag's own path, a pop is
    // impossible); -fsRamp 0 at t0 = the instant jump, kept as the pop control.
    public static float ForeshortenRampAt { get; private set; } = -1f;
    public static float ForeshortenRamp { get; private set; } = 0f;

    // WALK CYCLE 67 (the Opus verdict's §3 cleanup, 24.09): -fsRampUnsafe <0|1>.
    // The runtime ramp is NOT safe while PoseDriveController.pelvisCenterToSole
    // stays a build-time frame (computed ONCE in Initialize, :786, used per frame
    // at :984 as groundY = standPelvisY - pelvisCenterToSole): a length that MOVES
    // under the standing body drifts that derived ground by the shortening, the
    // drive reads the body as SINKING and lifts it, the feet unload and the body
    // falls within 3-5 s at every rate (c66 measured 2.15 / 8.6 / 11.5 cm/s and
    // the instant jump). So the runtime values are REFUSED unless this switch is
    // passed: without it the foreshorten applies AT BUILD (the channel's own safe
    // path - a body born short stands 40 s) and the refusal is logged. Pass
    // -fsRampUnsafe 1 to force the runtime channel (the c66 behaviour, pop control
    // and rate probes included). Default 0 = the guard; the flag-off rows are
    // untouched by construction (nothing here reads a value when Foreshorten = 0).
    public static bool ForeshortenRampUnsafe { get; private set; } = false;

    // Safety cap: if physics hangs, the process must not live forever.
    private const float REAL_TIME_LIMIT_SECONDS = 600f;
    private static readonly WaitForFixedUpdate FixedStep = new WaitForFixedUpdate();

    private float duration = 10f;
    private string label = "trial";
    private string outputFolder;
    private bool visual;
    private float startY = -0.82f;

    // Torso push: checks whether the controller holds a disturbance, not only
    // itself. Without it, equilibrium may be just a static pose.
    private float pushTime;
    private float pushImpulse;
    private Rigidbody2D torsoBody;

    // Crouch via MotionIntent, not the keyboard: batchmode has none.
    private float crouchTarget;
    private float crouchTime;
    private float crouchHold;
    // A4: scripted squat-and-rise - the same intent.crouch channel, scheduled as
    // one down-and-up cycle while the body stands (see UpdateCrouchIntent).
    private float squatRise;
    private float squatRiseTime;
    private float squatRiseHold;
    // SQUAT TASK (cycle 84): a REP COUNT on the same intent.crouch channel. While the
    // window is open the trial asks for crouch = 1 AND squatReps = N; the task machine
    // inside BalanceController turns that into N stand->squat->stand cycles (its own
    // gates decide when a rep counts) and stops after the Nth. 0 = the plain hold every
    // earlier schedule uses.
    private int squatReps;
    private float squatTaskTime;
    private float squatTaskHold;
    // SQUAT TASK (cycle 85): the round's two doses. -squatPace 0 runs the c84 clock (the
    // control arm of the load term); -squatBottomHold h overrides the bottom dwell.
    // Defaults are the shipped law (pacing on, the controller's own dwell), so every
    // earlier schedule and the editor stay untouched.
    private int squatPace;
    private float squatBottomHold;
    // CYCLE 86 doses: the rise's own term (the release rate's flat scale, and the load
    // gate). Both 0 = the shipped law.
    private float squatReleaseScale;
    private int squatReleaseLoad;
    // CYCLE 85b belts: the sink bypass (m/s) and the gate floor (0..1). Both 0 = shipped.
    private float squatReleaseSink;
    private float squatReleaseFloor;
    // CYCLE 87 doses: the BOTTOM's own term. -squatBottomGate 1 arms the state-driven
    // bottom exit; -squatBottomSettle s is its guard and -squatBottomMps v the arrest
    // threshold (both 0 = the controller's own values). All default to the shipped law.
    private int squatBottomGate;
    private float squatBottomSettle;
    private float squatBottomMps;
    // CYCLE 90 doses: the rise's CoM admittance (band m; the scale defaults to a full
    // hold and the cap to the controller's own) and the arm raise (the owner's gesture:
    // the trial owns only the gate level, see MotionIntent.armRaise*). All default OFF -
    // the shipped law on every earlier path.
    private float squatRiseCom;
    private float squatRiseComScale;
    private float squatRiseComMax;
    private int armRaise;
    private float armRaiseTime = 8f;
    private float armRaiseShoulderDeg = -68f;
    private float armRaiseElbowDeg = -6f;
    private float armRaiseBendDeg = -50f;
    private float armRaiseHold = 0.35f;
    private int armRaiseReps = 1;
    private float armRaisePeriod = 3f;
    private float leanTarget;
    private float leanTime;
    private float leanHold;
    // Arm strike (punch cycle p1): the request window, written to
    // MotionIntent.punch - the same field the editor's F key writes.
    private float punchTarget;
    private float punchTime = 5f;
    private float punchHold = 10f;
    // Punch cycle p3 (the recovery rung): with a positive pause the hold window
    // above fires TWICE, the second burst starting punchHold + punchPause after
    // the first ("two bursts with a pause"). 0 = the p1/p2 single window, the
    // landed shape, bit for bit.
    private float punchPause;
    // Punch cycle p4 (the third burst / other burst lengths): an explicit burst
    // schedule - `-punchBursts "9,9,9"` = that many window lengths in seconds,
    // each burst starting punchPause after the previous one ends. Empty (the
    // default) = the landed shapes above, bit for bit.
    private float[] punchBurstLens;
    // Leg strike (kick cycle k1): the request window, written to
    // MotionIntent.kick - the same field the editor's G key writes.
    private float kickTarget;
    private float kickTime = 5f;
    private float kickHold = 10f;
    private MotionIntent intent;

    // One-leg stance: −1 left support, +1 right, 0 both. Same as crouch —
    // the intent is written every step so live input cannot erase the script.
    private float standLegTarget;
    private float standLegTime;
    private float standLegHold;

    // March in place: clocked standLeg, not walk / WalkLift / SIMBICON.
    private float marchTarget;
    private float marchTime = 5f;
    private float marchDuration = 24f;
    private float marchHold = 2.5f;
    private float marchPlant = 1.5f;
    private float marchFirst = -1f;

    // Support swap: reuse StepPhaseDriver, not SIMBICON.
    private float walkTarget;
    private float walkTime;
    private float walkDuration;
    // FLAT SEQUENCE (cycle 96): the control plan. -walkPlan "w6,i2,w6,i2,s5,r2,w8"
    // replaces the single walk window with a desk schedule: w = walk, i = idle stand,
    // s / r = the squat cycle's desk (the s*r stretch arms the squat task machine; its
    // own gates sequence descend/bottom/rise - the halves carry only the timing). The
    // empty string (default) is every earlier take, bit for bit. ParseWalkPlan owns
    // the syntax and the fallback.
    private string walkPlanRaw = "";
    private char[] walkPlanKind = new char[0];
    private float[] walkPlanStart = new float[0];
    private float[] walkPlanEnd = new float[0];
    private float walkPlanTotal;
    private int walkPlanReps = 1;
    // CYCLE 97: the stop brake (see WalkPlanBraking). A w desk's end stops the
    // walk in one tick today (EndWalk) and the momentum topples the body (c96
    // plan_d: boundary at 7.5 s, fall at 8.4). With the brake the walk keeps
    // stepping for walkPlanBrake seconds while moveX ramps 1 -> walkPlanBrakeFloor:
    // the walk's CoM lead target scales with |moveX| (BalanceController's
    // leadMag), so the body sheds speed while the gait can still catch it. 0 = off.
    private float walkPlanBrake;
    private float walkPlanBrakeFloor;
    // CYCLE 97b: the brake's deceleration seat - the walk's own braking catch
    // (PoseDriveController.ReflexCatchShare; 0.5 = the landed law, 1 = the arrest
    // point). Written only while a brake runs, so every other take is bit for bit.
    private float walkPlanBrakeCatch = 0.5f;
    // CYCLE 124: the fit rule - a brake never cuts mid-ramp. Armed (default), a desk
    // shorter than the brake window re-paces the ramp to finish exactly with the desk
    // (the drop lands at the walkPlanBrakeFloor speed, not mid-ramp); 0 = the old cut.
    private float walkPlanBrakeFit = 1f;
    // DELEGATION req-272: player keys drive the plan (-level1Keys 1, default off).
    // A key does not add a code path: it EDITS the plan (CutPlanAt + PushDesk), so
    // every plan reader stays the one source of truth. Keys are read in FixedUpdate
    // as held state; an edge is one 5 ms step, the desk resolution the replay needs.
    private bool keysOn, level1KeysFlag, keySquatBrakeWalk;
    private string keyReplayPath = "", keyMaskScriptPath = "";
    private float level1KeysIdle;
    private bool keysTakenOver, keySquatLatch, keySquatQueued, keyRecover;
    private float keyTakeoverT = -1f, keySquatLatchT, keyQuietT, keyRecoverT;
    private int keyMaskPrev;
    private StreamWriter keyLog;
    private Rigidbody2D keyBody;
    private BalanceController keyBalance;
    private int keyEdges, keySquatDesks, keyQueuedDesks, keyFallenRows, keyReplayIdx;
    private List<KeyRow> keyReplayRows = new List<KeyRow>();
    private List<MaskRow> keyMaskScript = new List<MaskRow>();
    private struct KeyRow { public int step; public float t; public char desk; public string src; public int mask; }
    private struct MaskRow { public float t0; public float t1; public int mask; }
    private PoseDriveController poseDrive;
    private bool brakeCatchWrote;
    private float walkStanceDuration;
    private float runTarget;
    private float runTime;
    private float runHold;
    private float runStanceDuration;
    private float runTransferMaxDuration;
    private float walkHeelStrikeMinAge = 0f;
    private float walkHeelAirMin = 0f;
    private float walkComTrigger = 0f;
    private float walkComTriggerMinAge = 0.35f;
    private float walkComTriggerPerSpeed = 0f;
    private float walkCadenceStanceGain = 0f;
    private float walkCadenceMinStance = 1.0f;
    private float walkCadenceTransferGain = 0f;
    private float walkCadenceMinTransfer = 0.5f;
    private float walkTransferMinDuration = 0.7f;
    private float walkTransferMaxDuration;
    private float walkTransferTouchdownMaxAge = 1.2f;
    private float walkTransferComMax;
    private float walkTransferFallbackComMax;
    private float walkTransferLevelMax;
    private float walkFirstStance;
    private int useFallCatchWalk;
    private float fallComLead;

    // ─── DEMO WALK (27.09, task "Evolutionary demo walk") ───
    // -demoWalk 1: the trial ground is REPLACED by the yard tape - the same
    // LevelTest1.PiecesForChunk pieces and the same 16 m chunk streaming the
    // World / Level1 scene runs - and the walker spawns where the shipped page
    // spawns it (LevelTest1.PlayerX, the GameProcess path's own startX; -8 was
    // the drawn-level spawn and a different condition). The engine's own "Ground" object is NOT built: the yard has none
    // either (GameProcess skips BuildGround for the yard), and the yard's
    // apple / fences / campfire are picture-only (no colliders).
    private bool demoWalk;
    private float demoShift;
    private float demoSpawnX = LevelTest1.PlayerX;
    private int demoKeepBehind = 2;
    private int demoKeepAhead = 3;
    private Transform demoLevelRoot;
    // LEVEL 1 (req-217 step 2): "-level1 1" builds the yard tableau (the Level1.unity
    // content: the drawn grove, the campfire, the decor marks and the crow flock) ON
    // the demo tape with the walker. Absent = every byte of this block stays unread.
    private bool level1Wanted;
    private bool level1CrowLife;
    // req-316: the soft yard band (level only, default off). Needs -level1CrowLife.
    private bool level1YardBound;
    private float level1YardMin = -14f;
    private float level1YardMax = 10f;

    // req-319 (B-FLOCK phase 1, default off): each level crow fears on its own;
    // the dog is a second threat only under its own sub-flag.
    private bool level1CrowFear;
    private bool level1CrowFearDog;

    // req-331: the perch pad window + own hop (needs -level1CrowLife; off = old bytes).
    private bool level1Perch;

    // LEVEL 1 STORYTELLER CAMERA (req-246): picture only, -level1Cam 1.
    private bool level1CamWanted;
    private Level1StoryCam level1StoryCam;
    private PlantTree level1AppleTree;
    private StringBuilder level1CamRows;
    private int level1CamLastShot = -1;
    // req-265: -level1CamSteady 1 draws the story pose every LateUpdate (not only at the
    // shots) and lets the story camera win over -camAuto. Absent = every byte as before.
    private bool level1CamSteady;
    private Level1CamSteady level1CamDraw;
    private bool level1CamSteadyShot;
    private Bird[] level1Crows;
    private Rigidbody2D[] level1CrowBodies;
    private Rigidbody2D level1HeroBody;
    private BirdFlockDrive level1FlockDrive;
    private const float Level1CrossX = -4.2f;     // the hero's approach line (the judge's)
    private const float Level1NearMeters = 4.2f;  // "within" for the crow answer
    private const float Level1HedgehogX = -11f;   // OFF the walk path (the constant stays)
    private readonly StringBuilder level1Rows = new StringBuilder();
    private float level1CrossT = -1f;
    private float level1MinDist = -1f;
    // req-237 Part 1 diagnosis (read-only): the flock's own hero read, per-crow support
    // state from the sensors, and every takeoff with its cause. New files only; the
    // old _level1.csv stays as it was (mode-word `air`), so cl231 still compares.
    private StringBuilder level1DiagRows;
    private StringBuilder level1TakeoffRows;
    private int[] level1AirPrev;
    private float level1NextSeeLog;
    // LEVEL1 DOG (req-231): the owner's dog with its OWN AI; only -level1Dog builds it.
    private bool level1DogWanted;
    private Dog level1Dog;
    private DogDrive level1DogDrive;
    private float level1DogMinDist = -1f;
    private float level1DogFellT = -1f;
    // WORLD-1 (req-285): the living-world observer host. Built only under -world 1
    // (itself parsed only under -level1), so every other take keeps every byte.
    private bool worldWanted;
    private bool worldBenchWanted;
    private int worldSeedArg;
    private int worldLogLevel = 1;
    private WorldBrainHost worldHost;
    private readonly Dictionary<int, GameObject> demoChunks = new Dictionary<int, GameObject>();
    private readonly List<int> demoDrop = new List<int>(8);
    private int demoLastCenter = int.MinValue;
    private float fallComLeadTrigger;
    private float fallPelvisLean;
    private float fallForwardLeanScale;
    private float catchKneeFlex;
    private float catchLeadCapture;
    private float catchFootLeadMin;
    private float catchLeadCaptureMargin;
    private float reachKneeFlex;
    private float fallStanceVault;
    private float fallSwingThighHold;
    private float fallSwingHipLift;
    private float fallSwingAnkleLift;
    private float fallSwingAirAnkle;
    private float fallSwingAirHeel;
    private float fallSwingHeelPlant;
    private float fallSwingScrapeExtend;
    private float fallSwingCatchLandKnee;
    private float fallSwingPlantStanceKnee;
    // Plant mid-stance extension of the new stance knee. 0 = former frozen
    // landDeg for the whole phase.
    private float fallSwingPlantMidStanceExtend;
    private float fallSwingVaultRamp;
    private float fallSwingCatchVaultYield;
    private float fallSwingArmClock;
    private float fallSwingReachLiftClock;
    private float fallSwingReachArmClock;
    private float fallSwingCatchArmClock;
    private float fallSwingCatchExtendClock;
    private float fallSwingCatchReachResume;
    private float fallSwingCatchReachScrapeResume;
    private float fallSwingCatchReachScrapeLiftHold;
    private float fallSwingCatchReachScrapeLiftAirAge;
    private float fallSwingCatchReachScrapeVaultHold;
    private float fallSwingCatchReachScrapeVaultAirAge;
    private float fallSwingCatchReachScrapeArmHold;
    private float fallSwingCatchReachScrapeArmAirAge;
    private float fallSwingCatchReachScrapeBrakeHold;
    private float fallSwingCatchReachScrapeBrakeAirAge;
    private float fallSwingCatchReachScrapeYieldHold;
    private float fallSwingCatchReachScrapeYieldAirAge;
    private float fallSwingCatchReachScrapeExtendHold;
    private float fallSwingCatchReachScrapeExtendAirAge;
    private float fallSwingCatchReachVaultClock;
    private float fallSwingCatchReachVaultAge;
        private float fallSwingCatchReachArmClock;
        private float fallSwingCatchReachLiftClock;
        private float fallSwingCatchReachLiftAge;
        private float fallSwingCatchReachArmAge;
    private float fallSwingCatchAirVaultHold;
    private float fallSwingCatchAirVaultRemain;
    private float fallSwingCatchAirArmHold;
    private float fallSwingCatchAirExtendHold;
    private float fallSwingCatchScrapeExtendHold;
    private float fallSwingCatchScrapeExtendAirAge;
    private float fallSwingCatchScrapeExtendAirEnterHold;
    private float fallSwingCatchReachScrapeExtendEnterHold;
    private float fallSwingCatchAirLiftHold;
    private float fallSwingCatchScrapeLiftAirHold;
    private float fallSwingCatchScrapeLiftAirEnterHold;
    private float fallSwingCatchScrapeLiftAirCatchHold;
    private float fallSwingCatchScrapeLiftAirCatchFold;
    private float fallSwingCatchScrapeLiftAirEnterFold;
    private float fallSwingCatchScrapeLiftAirEnterArm;
    private float fallSwingCatchScrapeLiftAirCatchArm;
    private float fallSwingCatchScrapeLiftAirCatchEndArm;
    private float fallSwingCatchScrapeLiftAirCatchGroundArm;
    private float fallSwingCatchScrapeLiftAirCatchAirArm;
    private float fallSwingCatchScrapeLiftAirCatchAirEndArm;
    private float fallSwingCatchScrapeLiftAirCatchAirGroundArm;
    private float fallSwingCatchScrapeLiftAirEnterGroundArm;
    private float fallSwingCatchScrapeLiftAirEnterGroundFold;
    private float fallSwingCatchScrapeLiftAirCatchGroundFold;
    private float fallSwingCatchScrapeLiftAirCatchAirGroundFold;
    private float fallSwingCatchScrapeLiftAirCatchAirFold;
    private float fallSwingCatchScrapeLiftAirCatchAirEnterFold;
    private float fallSwingCatchScrapeLiftAirCatchAirEnterArm;
    private float fallSwingCatchScrapeLiftAirCatchAirEnterGroundArm;
    private float fallSwingCatchScrapeLiftAirCatchAirEnterGroundFold;
    private float fallSwingCatchScrapeLiftAirCatchAirEnterEndArm;
    private float fallSwingCatchScrapeLiftAirCatchAirEnterCatchArm;
    private float fallSwingCatchScrapeLiftAirCatchAirEnterCatchFold;
    private float fallSwingCatchScrapeLiftAirCatchAirEnterEndFold;
    private float fallSwingCatchScrapeLiftAirCatchEndFold;
    private float fallSwingCatchScrapeLiftAirCatchGroundCatchFold;
    private float fallSwingCatchScrapeLiftAirCatchGroundCatchEndFold;
    private float fallSwingCatchScrapeLiftAirCatchAirEndFold;
    private float fallSwingCatchScrapeLiftAirCatchAirGroundCatchFold;
    private float fallSwingCatchScrapeLiftAirCatchPlantExitFold;
    private float fallSwingPlantStanceExtendClock;
    private float fallSwingVaultFromLeftover;
    private float fallSwingFallAnklePush;
    private float fallSwingFallHipLiftCap;
    private float fallSwingFallAnkleLift;
    private float fallSwingFallHipLift;
    private float fallSwingReachAirHold;
    private float fallSwingFallAirHold;
    private float fallSwingFallHipLiftCapWide;
    private float fallSwingReachHipLiftCapWide;
    private float fallSwingCatchAirHipLiftCapWide;
    private float fallSwingPlantHipLiftCapWide;
    private float fallSwingFallHipLiftCapCeil;
    private float fallSwingReachHipLiftCapCeil;
    private float fallSwingCatchAirHipLiftCapCeil;
    private float fallSwingPlantHipLiftCapCeil;
    private float fallSwingCatchScrapeHipLiftCapCeil;
    private float fallSwingCatchReachScrapeHipLiftCapCeil;
    private float fallSwingCatchAirHold;
    private float fallSwingCatchAirEnterHold;
    private float fallSwingPlantAirHold;
    private float fallSwingReachAirSwingCeil;
    private float fallSwingHipairCeil;
    private float fallSwingRecoverHipairClear;
    private float fallSwingRecoverAirAnkleClear;
    private float fallSwingPlantAirAnkleClear;
    private float fallSwingPlantAirAnkleLevel;
    private float fallSwingRecoverAirKneeClear;
    private float fallSwingReachHipLiftCap;
    private float fallSwingReachHipLift;
    private float fallSwingReachAnkleLift;
    private float fallSwingReachHipLiftCapHold;
    private float fallSwingFallHipLiftCapHold;
    private float fallSwingFallVaultLead;
    private float fallSwingFallLiftLead;
    private float fallSwingFallFoldLead;
    private float fallSwingCatchAirHipLiftCap;
    private float fallSwingCatchAirHipLiftCapHold;
    private float fallSwingCatchAirHipLift;
    private float fallSwingCatchAirAnkleLift;
    private float fallSwingCatchScrapeHipLiftCap;
    private float fallSwingCatchScrapeHipLiftCapHold;
    private float fallSwingCatchScrapeHipLift;
    private float fallSwingCatchScrapeAnkleLift;
    private float fallSwingCatchScrapeLiftLead;
    private float fallSwingCatchScrapeHipLiftCapWide;
    private float fallSwingCatchReachScrapeHipLiftCap;
    private float fallSwingCatchReachScrapeHipLiftCapHold;
    private float fallSwingCatchReachScrapeHipLift;
    private float fallSwingCatchReachScrapeAnkleLift;
    private float fallSwingCatchReachScrapeHipLiftCapWide;
    private float plantMinDuration;
    private float plantLeadWaitSeconds;
    private float reachTimeout;
    private float fallAbortDeg;
    private float plantStallReseatSeconds;
    private float plantReseatLeadMargin;
    private float plantStallStandSeconds;
    private float plantClearRestoreOnGate;
    private float reachGroundStallSeconds;
    private float fallCatchBounceMax;
    private float sinkAbortDrop;
    private float plantAirLowestFoot;
    private float walkRecoverAnyPose;
    private StepPhaseDriver stepDriver;

    private Human human;
    private TrialRecorder recorder;
    private TrialRunInfo runInfo;
    // ARENA-1: the parameter-sweep arena (ArenaRunner.cs). In this mode the trial
    // player builds ground only and the arena spawns its own bodies.
    private bool spawnArena;
    private ArenaRunner arena;
    // DOG ARENA (cycle 64, 23.09.2026): the dog lane's own evolutionary flow (DogArenaRunner.cs).
    // The branch exists ONLY when -dogArena 1 is on the command line: nothing in this file reads
    // these fields otherwise, so the accepted path (-dog 1 -dogWalk 1 -dogWalkTime 1
    // -dogPawFriction 0.4) stays bit-identical with the branch absent. The dog law files are not
    // touched by this branch: the arena is its own code and its own scene.
    private bool spawnDogArena;
    private DogArenaRunner dogArena;
    private int dogArenaSeed = 1;
    private float dogArenaSeconds = 45f;
    private float dogArenaInterval = 0.5f;
    private float dogArenaMeasure = 15f;
    private float dogArenaCull = 30f;
    private float dogArenaWallLimit = 1800f;
    // cycle 65: the clean-field re-measure's genome (15 comma-separated values); empty = the
    // ordinary evolutionary run. The ONE additive arg of this round.
    private string dogArenaInject = "";
    // THE FIELD'S OWN ISOLATION (cycle 66): layer|pairs, passed straight through to the runner.
    private string dogArenaIsolation = "layer";
    private bool dogArenaStepping;
    private int dogArenaSteps;
    private int arenaSeed = 1;
    // WALK CYCLE 58: ArenaHumanAgent.LawQuality (the line's own band, default since c61),
    // LawLine (the c58 bars - the round's own before) or LawControl (the c54 shape over
    // the same window - the c58 round's control run).
    private int arenaLaw = ArenaHumanAgent.LawQuality;
    // WALK CYCLE 61: the band's own knobs (the round's sensitivity reads; the landed
    // values are the constants).
    private float arenaLiftFloor = ArenaHumanAgent.SweepLiftFloorMeters;
    private float arenaClearShare = ArenaHumanAgent.SweepClearShareBar;

    private float arenaInterval = 0.5f;
    private float arenaSeconds = 90f;
    private float arenaWallLimit = 3000f;
    private float arenaNumberHeight = 0.34f;
    private float arenaCameraSize = 5f;
    private float arenaCameraOffsetX = 2.5f;
    private float arenaPhotoSize = 2.2f;
    private float arenaPhotoOffsetX = 1.2f;
    private float arenaPhotoTime = 12f;
    private float arenaGroundMeters = 200f;
    // ARENA CYCLE 59: the take's own lens rule (see ArenaRunner.cameraFollowLeader).
    private bool arenaCamFollow = true;
    private float arenaLead = 2.5f;
    // BALANCE-ARENA-1: the impact harness (ImpactThrower.cs / ImpactRecorder.cs).
    // Off unless -impact 1. The world is the normal stand: ground + ONE human; the
    // only addition is the thrower, which spawns real Rigidbody2D bodies at him.
    private bool spawnImpact;
    private ImpactThrower impactThrower;
    private ImpactRecorder impactRecorder;
    private float impactMass = 4f;
    private string impactShape = "circle";
    private float impactSize = 0.25f;
    private float impactRectAspect = 2f;
    private float impactSpeed = 8f;
    private string impactAim = "torso";
    private string impactSide = "seed";
    private float impactAt = 8f;
    private int impactCount = 3;
    private float impactSpacing = 6f;
    private int impactSeed = 1;
    private float impactShotInterval = 0.2f;
    // The visual shot cadence (s per shot). The historical 0.2 s == 5 fps; the dog's
    // cinema runs ask for more (CLI -shotRate <fps>, default 5) so a get-up that lives
    // a few tenths of a second is visible at all.
    private float shotPeriod = 0.2f;
    private float impactWindow = 4.5f;
    private float impactCameraSize = 2.4f;
    private float impactCameraY = -1.0f;
    // LINE 2 — BALL HAIL (27.09, the owner's second line): tennis balls INTO the standing
    // human + the restitution record. BallHail.cs / BallImpactRecorder.cs; off unless
    // -ballhail 1. Another ADDITIVE mode: the world is the normal stand, plus a real
    // Rigidbody2D shooter. Nothing here touches the impact harness's own switches.
    private bool spawnBallHail;
    private BallHailShooter ballShooter;
    private BallImpactRecorder ballRecorder;
    private string ballShots = "head:front:5;chest:obliqueL:5;thigh:obliqueR:5;shin:topdown:4";
    private float ballMassKg = 0.30f;        // OWNER 27.09 ~17:45 ("масса мячиков больше сделай"):
                                             // new hail default 0.30 kg ~5x an ITF ball; the mass
                                             // ladder passes 0.0577 / 0.15 / 0.30 / 0.60 explicitly
    private float ballRadiusM = 0.0335f;     // tennis ball, ITF 6.54-6.86 cm diameter
    private float ballBounce = 0.75f;        // the ball's own restitution (config column)
    private float ballFriction = 0.4f;
    private float ballGravity = 0f;
    private float ballDistance = 2.5f;
    private float ballAt = 6f;
    private float ballSpacing = 3f;
    private float ballLife = 6f;
    private float ballSegBounce = -1f;       // < 0 = leave the rig's own materials alone
    private float ballSegFriction = 0.4f;
    private bool ballAimFix = true;          // Stage 2: the line-of-sight pre-check
    private float ballAimRange = 0.12f;
    private float ballAimStep = 0.01f;
    private bool ballAimCast = true;         // b3: sweep the ball's own radius, not a zero-width ray
    // b4 (27.09): the reachability fix - a DISC of aim candidates around the segment's
    // anchor (0 = the b3 one-axis slide) and the stand's own geometry dump ("" = the
    // take's own file: <out>/<label>_rigdump.csv).
    private float ballAimSearch = 0f;
    private string ballRigDump = "";
    private float ballCameraSize = 2.6f;
    private float ballCameraY = -1.0f;
    // B-NAT-0 film framing (capture only): the lens that follows the recorded bird.
    private float birdCameraSize = 3f;
    private float birdCameraOffsetY = 0.55f;
    // Owner's rule (Yury, 25.09): -camAuto 1 makes the test scenes' frame follow the OBJECTS'
    // combined extent (CameraAutoFit) instead of any fixed lens. Off by default: physics rows
    // and the lanes' established takes keep their exact behaviour.
    private bool camAuto = false;
    private float camAutoMargin = 0.5f;
    // WALK CYCLE 56 — the evidence take's static lens (capture only). 0 = off: every existing
    // take keeps its own framing (CameraFollow). No physics row ever reads it: a -nographics
    // run builds no camera at all, and the vis row's CSV is md5-checked against the physics row.
    private float capLensSize = 0f;
    private float capLensX = 0f;
    private float capLensY = -3f;
    private bool spawnBird;
    private bool spawnTree;
    private bool spawnFlower;
    private bool treePerch;
    private PlantTree plantTree;
    private PlantTrialRecorder plantRecorder;
    private bool spawnApple;
    private AppleTree appleTree;
    private AppleTrialRecorder appleRecorder;
    private PlantFlower[] trialFlowers;
    private FlowerTrialRecorder flowerRecorder;
    private bool spawnDog;
    private bool spawnDogBird;
    private float dogBirdX = 2f;
    private Dog dog;
    private DogTrialRecorder dogRecorder;
    private bool dogLeap;
    private float dogLeapTime = 2f;
    private float dogPreyX = 1.4f;
    private bool dogWalk;
    private float dogWalkTime = 1f;
    // DOG STEP CLOCK (harness only, 19.09): the dog trial is driven by EXECUTED
    // physics steps, not by coroutine iterations. A coroutine resumed on
    // `FixedStep` (WaitForFixedUpdate) runs once per FRAME, while Unity executes a
    // variable number of FixedUpdate calls per frame: measured 5002..5007 executed
    // steps against 5001 nominal samples on ONE build, which is exactly why the
    // "bit-identical" dog_art1 triple was not reproducible
    // (Trials/Runs/dog-art1-bisect-report.md). ImpactArena.FixedUpdate already
    // counts its own steps; the dog trial now does the same, so a run IS its step
    // count: duration / fixedDeltaTime steps, samples == executedSteps.
    private bool dogStepping;
    private int dogSteps;
    private bool dogPushed;
    private bool dogLeaped;
    private bool dogWalked;
    private bool dogStopped;
    private float dogWalkStopTime;
    // HUMAN STEP CLOCK (harness only, 21.09): the dog trial's pattern above,
    // adopted by the human trial for the same reason - a coroutine resumed on
    // `FixedStep` advances once per FRAME, while Unity executes a variable number
    // of FixedUpdate calls per frame, so the rendered and the headless take of
    // one protocol were two different runs (97 physics keys apart on the graded
    // law, Trials/Runs/walk-cycle17-clock-pair.md). The human clock, its
    // scheduled intents and the recorder sample now live in FixedUpdate, one per
    // EXECUTED physics step; RunTrial's coroutine only waits for the step budget,
    // captures frames and writes the result.
    private bool humanStepping;
    private int humanSteps;
    private bool humanPushed;
    private bool humanCrouchMarked;
    private bool humanCrouchReleased;
    private bool humanLeanMarked;
    private bool humanLeanReleased;
    private bool humanStandLegMarked;
    private bool humanStandLegReleased;
    // DROP TEST (owner's line 3, 27.09): -drop <h> lifts the WHOLE standing rig h
    // metres at -dropTime with every velocity zeroed, and then does nothing - the
    // fall, the landing and the recovery are the balance core's own, with no
    // landing hook and no special case anywhere below this switch. 0 = off, so
    // every existing run (walk / squat / punch / stance) keeps its exact schedule.
    private float dropHeight;
    private float dropTime = 6f;
    private bool humanDropped;
    private float dropReleaseTime = -1f;
    private int dropReleaseStep = -1;
    private int dropBodyCount;
    // TILT-PLATFORM STAND (29.09, the owner's stand): -tiltPlatform 1 builds the
    // rotating deck under the standing man (TiltPlatform.cs) and turns on the
    // per-step log (TiltStandLog.cs). With no flag NOTHING here runs: no deck, no
    // ground change, no extra row - every existing take keeps its bytes.
    private bool tiltWanted;
    private TiltPlatform tiltStand;
    private TiltStandLog tiltLog;
    private TiltLean tiltRide;
    // ROUND 13 (the owner's addendum 6, the get-up practice): -tiltGetUp 1 presses
    // the man's own get-up input the moment the fall criterion fires (the pose
    // drive's ladder: palms -> trunk over the hands -> knees under -> rise; see the
    // per-frame driver), and the take no longer ends 2.5 s after the fall.
    // -tiltGetUpHold 1 freezes the deck's ramp at the fall (a steady deck to
    // practice on). Both OFF by default: every pre-existing tilt row keeps its bytes.
    private bool tiltGetUpWanted;
    // ROUND 15 (the plant-phase ejection): -tiltThickness = the deck plate's collision
    // thickness (0 = the drawn 0.06 m); -tiltPen > 0.5 = the palm-penetration trace.
    private float tiltThicknessArg;
    private bool tiltPenWanted;
    private bool tiltGetUpHoldWanted;
    // ROUND 35: -tiltGetUpHoldEase > 0 eases the frozen deck to level at this rate
    // (deg/s; 0 = the r13 freeze-at-fall); -tiltVoidEnd 1 ends a practice take once
    // the body has left the decks. Both default off.
    private float tiltGetUpHoldEaseDeg;
    private bool tiltVoidEndWanted;
    // ROUND 40: the deck's freeze window (the r39 push discriminator), s.
    private float tiltFreezeFromArg = -1f;
    private float tiltFreezeLenArg;
    private bool humanGetUpKey;
    private Bird bird;
    private Bird[] trialBirds;
    private BirdFlockDrive trialFlockDrive;
    private BirdTrialRecorder birdRecorder;
    private Rigidbody2D birdBody;
    private float stopFlapTime;
    private string stopFlapMode;
    private string birdDriveKind;
    private BirdKind trialBirdKind = BirdKind.Crow;
    private int birdDriveCount = 1;
    private int birdDriveSeed = 1;
    private const float BirdPackSpacing = 1.2f;

    [Serializable]
    private class BuildManifestDto
    {
        public string fingerprint;
        public string algorithm;
        public string unityVersion;
        public string unityPath;
        public string buildTimeUtc;
        public bool repositoryPresent;
        public bool gitAvailable;
        public string gitCommit;
        public bool gitDirty;
        public string buildGuid;
        public string outputPath;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        string[] args = Environment.GetCommandLineArgs();
        if (Array.IndexOf(args, ArgsAudit.Note("-trial")) < 0) return;

        Active = true;
        VisualCapture = Array.IndexOf(args, ArgsAudit.Note("-visual")) >= 0
            && Array.IndexOf(args, ArgsAudit.Note("-nographics")) < 0;
        RigOverlay = GetFloatArg("-rigOverlay", 1f) > 0.5f;
        SkinJoints = GetFloatArg("-skinJoints", 0f) > 0.5f;
        CoverJoints = GetFloatArg("-coverJoints", 1f) > 0.5f;
        Foreshorten = GetFloatArg("-foreshorten", 0f);
        string fsMass = GetStringArg("-fsMass", "hold");
        ForeshortenMassProjected = fsMass == "project" || fsMass == "projected";
        ForeshortenRampAt = GetFloatArg("-fsRampAt", -1f);
        ForeshortenRamp = GetFloatArg("-fsRamp", 0f);
        // WALK CYCLE 67 THE GUARD: the runtime channel is refused unless the caller
        // asks for it explicitly (see ForeshortenRampUnsafe above). The refusal is
        // normalised HERE, at parse time, so every reader downstream (Human.
        // ForeshortenPrepare) sees the build-time path and no second code path
        // exists. -fsRampUnsafe 1 restores the c66 runtime behaviour byte for byte.
        ForeshortenRampUnsafe = GetFloatArg("-fsRampUnsafe", 0f) > 0.5f;
        if (Foreshorten > 0f && ForeshortenRampAt >= 0f && !ForeshortenRampUnsafe)
        {
            Debug.LogWarning("UNSAFE: runtime foreshorten, stale pelvisCenterToSole "
                + "(PoseDriveController.pelvisCenterToSole, Initialize :786 used :984) - "
                + "-fsRampAt/-fsRamp REFUSED; the value applies AT BUILD instead. "
                + "Pass -fsRampUnsafe 1 to force the runtime channel.");
            ForeshortenRampAt = -1f;
            ForeshortenRamp = 0f;
        }

        ConfigureDeterministicPhysics();

        // Solver floor — CLI sweep of cycle 5 only; production stays 8/3.
        int velocityIterations = Mathf.RoundToInt(GetFloatArg("-velocityIterations", Physics2D.velocityIterations));
        int positionIterations = Mathf.RoundToInt(GetFloatArg("-positionIterations", Physics2D.positionIterations));
        if (velocityIterations > 0)
            Physics2D.velocityIterations = velocityIterations;
        if (positionIterations > 0)
            Physics2D.positionIterations = positionIterations;

        // Set the physics step before the scene loads: otherwise the first FixedUpdate
        // still runs at the default 0.02 s, and frequency comparisons go crooked.
        float fixedDelta = GetFloatArg("-fixedDelta", Time.fixedDeltaTime);
        if (fixedDelta > 0f)
            Time.fixedDeltaTime = fixedDelta;

        GameObject host = new GameObject("HeadlessTrial");
        DontDestroyOnLoad(host);
        host.AddComponent<HeadlessTrial>();
    }

    // The stand compares runs bitwise: without this, PhysX 2D on the edge of
    // contacts (right support) makes the same exe give fell / not fell.
    private static void ConfigureDeterministicPhysics()
    {
        UnityEngine.Random.InitState(0);

        PhysicsJobOptions2D options = Physics2D.jobOptions;
        options.useMultithreading = false;
        options.useConsistencySorting = true;
        Physics2D.jobOptions = options;
    }

    private void Start()
    {
        duration = GetFloatArg("-duration", 10f);
        label = GetStringArg("-label", "trial");
        outputFolder = GetStringArg("-out", DefaultOutputFolder());
        visual = HasArg("-visual");
        if (visual && !VisualCapture)
        {
            Debug.LogWarning("HeadlessTrial: -visual skipped under -nographics.");
            visual = false;
        }
        spawnArena = GetFloatArg("-arena", 0f) > 0.5f;
        arenaSeed = Mathf.RoundToInt(GetFloatArg("-arenaSeed", arenaSeed));
        arenaLaw = Mathf.RoundToInt(GetFloatArg("-arenaLaw", (float)arenaLaw));
        arenaLiftFloor = GetFloatArg("-arenaLiftFloor", arenaLiftFloor);
        arenaClearShare = GetFloatArg("-arenaClearShare", arenaClearShare);
        arenaInterval = GetFloatArg("-arenaInterval", arenaInterval);
        arenaSeconds = GetFloatArg("-arenaSeconds", arenaSeconds);
        arenaWallLimit = GetFloatArg("-arenaWallLimit", arenaWallLimit);
        arenaNumberHeight = GetFloatArg("-arenaNumber", arenaNumberHeight);
        arenaCameraSize = GetFloatArg("-arenaCamera", arenaCameraSize);
        arenaCameraOffsetX = GetFloatArg("-arenaCamX", arenaCameraOffsetX);
        arenaPhotoSize = GetFloatArg("-arenaPhoto", arenaPhotoSize);
        arenaPhotoOffsetX = GetFloatArg("-arenaPhotoX", arenaPhotoOffsetX);
        arenaPhotoTime = GetFloatArg("-arenaPhotoTime", arenaPhotoTime);
        arenaGroundMeters = GetFloatArg("-arenaGroundMeters", arenaGroundMeters);
        // ARENA CYCLE 59: the lens rule's own switches (defaults = the follow on).
        arenaCamFollow = GetFloatArg("-arenaCamFollow", arenaCamFollow ? 1f : 0f) > 0.5f;
        arenaLead = GetFloatArg("-arenaLead", arenaLead);
        spawnDogArena = GetFloatArg("-dogArena", 0f) > 0.5f;
        dogArenaSeed = Mathf.RoundToInt(GetFloatArg("-dogArenaSeed", dogArenaSeed));
        dogArenaSeconds = GetFloatArg("-dogArenaSeconds", dogArenaSeconds);
        dogArenaInterval = GetFloatArg("-dogArenaInterval", dogArenaInterval);
        dogArenaMeasure = GetFloatArg("-dogArenaMeasure", dogArenaMeasure);
        dogArenaCull = GetFloatArg("-dogArenaCull", dogArenaCull);
        dogArenaWallLimit = GetFloatArg("-dogArenaWallLimit", dogArenaWallLimit);
        dogArenaInject = GetStringArg("-dogArenaInject", dogArenaInject);
        dogArenaIsolation = GetStringArg("-dogArenaIsolation", dogArenaIsolation);
        spawnImpact = GetFloatArg("-impact", 0f) > 0.5f;
        impactMass = GetFloatArg("-impactMass", impactMass);
        impactSpeed = GetFloatArg("-impactSpeed", impactSpeed);
        impactAt = GetFloatArg("-impactAt", impactAt);
        impactCount = Mathf.RoundToInt(GetFloatArg("-impactCount", impactCount));
        impactSpacing = GetFloatArg("-impactSpacing", impactSpacing);
        impactSeed = Mathf.RoundToInt(GetFloatArg("-impactSeed", impactSeed));
        impactShotInterval = GetFloatArg("-shotInterval", impactShotInterval);
        shotPeriod = 1f / Mathf.Max(1f, GetFloatArg("-shotRate", 5f));
        birdCameraSize = GetFloatArg("-birdCam", birdCameraSize);
        birdCameraOffsetY = GetFloatArg("-birdCamY", birdCameraOffsetY);
        camAuto = GetFloatArg("-camAuto", 0f) > 0.5f;
        camAutoMargin = GetFloatArg("-camAutoMargin", camAutoMargin);
        impactWindow = GetFloatArg("-impactWindow", impactWindow);
        impactCameraSize = GetFloatArg("-impactCamera", impactCameraSize);
        impactCameraY = GetFloatArg("-impactCameraY", impactCameraY);
        // WALK CYCLE 56: the evidence take's static lens — a wide frame that shows a walker
        // crossing the old 10 m edge. Capture only (see the field's comment).
        capLensSize = GetFloatArg("-capLens", capLensSize);
        capLensX = GetFloatArg("-capLensX", capLensX);
        capLensY = GetFloatArg("-capLensY", capLensY);
        // -impactShape circle|square|rect [size_m]: the card writes the size as the
        // shape's own argument, so a numeric token right after the shape is read as
        // the size, and -impactSize keeps working on its own.
        string[] impactArgv = Environment.GetCommandLineArgs();
        for (int impactArgIndex = 0; impactArgIndex < impactArgv.Length - 1; impactArgIndex++)
        {
            if (!string.Equals(impactArgv[impactArgIndex], "-impactShape",
                StringComparison.OrdinalIgnoreCase)) continue;
            impactShape = impactArgv[impactArgIndex + 1];
            if (impactArgIndex + 2 < impactArgv.Length)
            {
                float trailing;
                if (float.TryParse(impactArgv[impactArgIndex + 2], NumberStyles.Float,
                    CultureInfo.InvariantCulture, out trailing) && trailing > 0f)
                    impactSize = trailing;
            }
        }
        impactSize = GetFloatArg("-impactSize", impactSize);
        impactAim = GetStringArg("-impactAim", impactAim).Trim().ToLowerInvariant();
        impactSide = GetStringArg("-impactSide", impactSide).Trim().ToLowerInvariant();
        // LINE 2 — BALL HAIL (27.09): the shot table and the ball's own constants.
        spawnBallHail = GetFloatArg("-ballhail", 0f) > 0.5f;
        ballShots = GetStringArg("-ballShots", ballShots);
        ballMassKg = GetFloatArg("-ballMass", ballMassKg);
        ballRadiusM = GetFloatArg("-ballRadius", ballRadiusM);
        ballBounce = GetFloatArg("-ballBounce", ballBounce);
        ballFriction = GetFloatArg("-ballFriction", ballFriction);
        ballGravity = GetFloatArg("-ballGravity", ballGravity);
        ballDistance = GetFloatArg("-ballDistance", ballDistance);
        ballAt = GetFloatArg("-ballAt", ballAt);
        ballSpacing = GetFloatArg("-ballSpacing", ballSpacing);
        ballLife = GetFloatArg("-ballLife", ballLife);
        ballSegBounce = GetFloatArg("-ballSegBounce", ballSegBounce);
        ballSegFriction = GetFloatArg("-ballSegFriction", ballSegFriction);
        ballAimFix = GetFloatArg("-ballAimFix", ballAimFix ? 1f : 0f) > 0.5f;
        ballAimRange = GetFloatArg("-ballAimRange", ballAimRange);
        ballAimStep = GetFloatArg("-ballAimStep", ballAimStep);
        ballAimCast = GetFloatArg("-ballAimCast", ballAimCast ? 1f : 0f) > 0.5f;
        ballAimSearch = GetFloatArg("-ballAimSearch", ballAimSearch);
        ballRigDump = GetStringArg("-ballRigDump", ballRigDump);
        ballCameraSize = GetFloatArg("-ballCamera", ballCameraSize);
        ballCameraY = GetFloatArg("-ballCameraY", ballCameraY);
        spawnBird = GetFloatArg("-bird", 0f) > 0.5f;
        spawnTree = GetFloatArg("-tree", 0f) > 0.5f;
        spawnFlower = GetFloatArg("-flower", 0f) > 0.5f;
        spawnApple = GetFloatArg("-apple", 0f) > 0.5f;
        treePerch = GetFloatArg("-treePerch", spawnTree && spawnBird ? 1f : 0f) > 0.5f;
        birdDriveKind = GetStringArg("-birdDrive", "").Trim().ToLowerInvariant();
        trialBirdKind = Bird.ParseKind(GetStringArg("-birdKind", "crow"));
        birdDriveCount = Mathf.Max(1, Mathf.RoundToInt(GetFloatArg("-birdCount", 1f)));
        birdDriveSeed = Mathf.RoundToInt(GetFloatArg("-birdDriveSeed", 1f));
        spawnDog = GetFloatArg("-dog", 0f) > 0.5f;
        spawnDogBird = GetFloatArg("-dogBird", 0f) > 0.5f;
        dogBirdX = GetFloatArg("-dogBirdX", 2f);
        dogLeap = GetFloatArg("-dogLeap", 0f) > 0.5f;
        dogLeapTime = GetFloatArg("-dogLeapTime", 2f);
        dogPreyX = GetFloatArg("-dogPreyX", 1.4f);
        dogWalk = GetFloatArg("-dogWalk", 0f) > 0.5f;
        dogWalkTime = GetFloatArg("-dogWalkTime", 1f);
        dogWalkStopTime = GetFloatArg("-dogWalkStop", 0f);
        // Hunt: a live bird in the dog trial, not a DogPrey mark.
        if (spawnDog && spawnDogBird)
            spawnBird = true;
        // Empty drive on a dog+bird scene is wander so Flee can Fly.
        if (spawnDog && spawnBird && string.IsNullOrEmpty(birdDriveKind))
            birdDriveKind = "wander";
        stopFlapTime = GetFloatArg("-stopFlap", 0f);
        stopFlapMode = GetStringArg("-stopFlapMode", "glide").Trim().ToLowerInvariant();
        // The bird sits lower than the human: −0.82 would leave it a metre in the air.
        // Dog: paws at y = −2.0, root at the chest centre.
        startY = spawnDog
            ? GetFloatArg("-startY", Dog.StandingRootY(-2f))
            : spawnBird
                ? GetFloatArg("-startY", Bird.KindStandingRootY(trialBirdKind, -2f))
                : GetFloatArg("-startY", -0.82f - HumanForeshortenLegDrop());
        pushImpulse = GetFloatArg("-pushImpulse", 0f);
        pushTime = GetFloatArg("-pushTime", 5f);
        crouchTarget = Mathf.Clamp01(GetFloatArg("-crouch", 0f));
        crouchTime = GetFloatArg("-crouchTime", 5f);
        crouchHold = GetFloatArg("-crouchHold", 10f);
        // A4 scripted squat-and-rise: one crouch-and-extend cycle while standing,
        // no walk and no new controller - it drives the same MotionIntent.crouch
        // target the -crouch switch drives, so the squat pose (hips + knees) and
        // its rates stay the ones the controller already owns. The extension is
        // the controller's own crouchReleaseRatePerSecond ramp (0.9/s = 1.11 s
        // for a full 1 -> 0), which is why -squatRiseHold 1 gives ~1 s of rise.
        squatRise = Mathf.Clamp01(GetFloatArg("-squatRise", 0f));
        squatRiseTime = GetFloatArg("-squatRiseTime", 5f);
        squatRiseHold = GetFloatArg("-squatRiseHold", 1f);
        // SQUAT TASK (cycle 84): -squatReps N arms the squat task machine for the window
        // [squatTaskTime, squatTaskTime + squatTaskHold). Default 0 = off, so every
        // earlier schedule (and the editor) is untouched.
        squatReps = Mathf.Max(0, Mathf.RoundToInt(GetFloatArg("-squatReps", 0f)));
        squatTaskTime = GetFloatArg("-squatTaskTime", 5f);
        squatTaskHold = GetFloatArg("-squatTaskHold", 30f);
        // CYCLE 85 doses: the shipped law is the c84 clock and the controller's own dwell
        // (both doses are priced, not landed - see BalanceController.squatBottomHoldSeconds).
        squatPace = Mathf.RoundToInt(GetFloatArg("-squatPace", 0f));
        squatBottomHold = Mathf.Max(0f, GetFloatArg("-squatBottomHold", 0f));
        // CYCLE 86 doses: the RETURN's own term. -squatReleaseScale s slows the rise's
        // release by a flat scale (0 = the plain clock), -squatReleaseLoad 1 gates that
        // rise by the load the soles carry. CYCLE 88: the gate is ARMED by default (the
        // c87 candidate landed); -squatReleaseLoad 0 -squatReleaseFloor 0
        // -squatBottomGate 0 give the c84 clock row again, byte for byte.
        squatReleaseScale = Mathf.Max(0f, GetFloatArg("-squatReleaseScale", 0f));
        squatReleaseLoad = Mathf.RoundToInt(GetFloatArg("-squatReleaseLoad", 1f));
        // CYCLE 85b belts: -squatReleaseSink mps takes the rise off the load gate while the
        // body is falling that fast; -squatReleaseFloor f keeps the rise creeping at f
        // whatever the gate reads. Both 0 = the shipped law.
        squatReleaseSink = Mathf.Max(0f, GetFloatArg("-squatReleaseSink", 0f));
        // CYCLE 88: the floor under the gate defaults to the landed candidate's 0.35
        // (c87bg40relf); the sink bypass stays off (priced, not needed by the candidate).
        squatReleaseFloor = Mathf.Max(0f, GetFloatArg("-squatReleaseFloor", 0.35f));
        // CYCLE 87/88: the BOTTOM's own term - the dwell becomes a floor and the exit
        // reads the body's own settle (see MotionIntent.squatBottomGate). CYCLE 88 LANDED
        // it: armed by default; the control arm is one line (with -squatReleaseLoad 0
        // -squatReleaseFloor 0).
        squatBottomGate = Mathf.RoundToInt(GetFloatArg("-squatBottomGate", 1f));
        squatBottomSettle = Mathf.Max(0f, GetFloatArg("-squatBottomSettle", 0f));
        squatBottomMps = Mathf.Max(0f, GetFloatArg("-squatBottomMps", 0f));
        // CYCLE 90: the rise's CoM admittance - the named target's own term (the return
        // after a fully settled bottom). -squatRiseCom m arms the band (0 = off); the
        // scale defaults to a full hold and the cap to the controller's own half second.
        squatRiseCom = Mathf.Max(0f, GetFloatArg("-squatRiseCom", 0f));
        squatRiseComScale = Mathf.Max(0f, GetFloatArg("-squatRiseComScale", 0f));
        squatRiseComMax = Mathf.Max(0f, GetFloatArg("-squatRiseComMax", 0f));
        // CYCLE 90: the arm raise (the owner's gesture, 28.09). The trial owns only the
        // GATE: the level rises at armRaiseTime on this trial's clock; the four-phase
        // curve, its targets and the hold are the controller's own (MotionIntent.armRaise*).
        armRaise = Mathf.RoundToInt(GetFloatArg("-armRaise", 0f));
        armRaiseTime = Mathf.Max(0f, GetFloatArg("-armRaiseTime", 8f));
        armRaiseShoulderDeg = GetFloatArg("-armRaiseShoulder", -68f);
        armRaiseElbowDeg = GetFloatArg("-armRaiseElbow", -6f);
        armRaiseBendDeg = GetFloatArg("-armRaiseBend", -50f);
        armRaiseHold = Mathf.Max(0f, GetFloatArg("-armRaiseHold", 0.35f));
        armRaiseReps = Mathf.RoundToInt(GetFloatArg("-armRaiseReps", 1f));
        armRaisePeriod = Mathf.Max(0.6f, GetFloatArg("-armRaisePeriod", 3f));
        leanTarget = Mathf.Clamp(GetFloatArg("-lean", 0f), -1f, 1f);
        leanTime = GetFloatArg("-leanTime", 5f);
        leanHold = GetFloatArg("-leanHold", 10f);
        // The arm strike (punch cycle p1). The trial writes the REQUEST only:
        // the stroke, the arm chain (shoulder/elbow/wrist) and the torque
        // authority are ArmDrive's own knobs (-punchWind/-punchExtend/
        // -punchReturn/-punchShoulderHit/...), and the balance layer keeps the
        // legs, the pelvis and the equilibrium throughout. The editor's F key
        // writes the same MotionIntent.punch field.
        punchTarget = Mathf.Clamp01(GetFloatArg("-punch", 0f));
        punchTime = GetFloatArg("-punchTime", 5f);
        punchHold = GetFloatArg("-punchHold", 10f);
        // Punch cycle p3: the seconds between the two bursts (0 = one window).
        punchPause = Mathf.Max(0f, GetFloatArg("-punchPause", 0f));
        // Punch cycle p4: the burst schedule (empty = the landed p1/p3 shapes).
        punchBurstLens = ParsePunchBurstLens(GetStringArg("-punchBursts", ""));
        // The leg strike (kick cycle k1). Same split as the punch above: the
        // trial writes the REQUEST only, the stroke and its authority are
        // LegStrikeDrive's own knobs (-kickLift/-kickChamber/-kickExtend/
        // -kickReturn/-kickInterval/-kickSide/-kickHipLift/-kickKneeChamber/
        // -kickKneeHit/-kickAnkleHit/-kickTorque/-kickGain/...). The editor's G
        // key writes the same MotionIntent.kick field.
        kickTarget = Mathf.Clamp01(GetFloatArg("-kick", 0f));
        kickTime = GetFloatArg("-kickTime", 5f);
        kickHold = GetFloatArg("-kickHold", 10f);
        standLegTarget = ParseStandLegArg(GetStringArg("-standLeg", "0"));
        standLegTime = GetFloatArg("-standLegTime", 5f);
        standLegHold = GetFloatArg("-standLegHold", 10f);
        marchTarget = Mathf.Clamp01(GetFloatArg("-march", 0f));
        marchTime = GetFloatArg("-marchTime", 5f);
        marchDuration = GetFloatArg("-marchDuration", 24f);
        marchHold = GetFloatArg("-marchHold", 2.5f);
        marchPlant = GetFloatArg("-marchPlant", 1.5f);
        marchFirst = ParseStandLegArg(GetStringArg("-marchFirst", "left"));
        if (Mathf.Abs(marchFirst) < 0.5f)
            marchFirst = -1f;

        walkTarget = Mathf.Clamp01(GetFloatArg("-walk", 0f));
        // The stand before a walk is a NUMBER, not a wait: the drop-in is settled
        // and the CoM velocity is 0 by 1.25 s (measured), so the walk default of
        // 1.5 s keeps a real quiet stand and still starts the walk in the first
        // seconds of the run. A trial that does NOT walk keeps the plain 5 s
        // default: the recorder's quiet window follows RequestedWalkTime (see
        // TrialRecorder), so stance / march / crouch / oracle records do not
        // move. Explicit -walkTime still overrides either default.
        walkTime = GetFloatArg("-walkTime", walkTarget > 0.5f ? 1.5f : 5f);
        // 38.5 keeps a walk window open to the end of a default 40 s run
        // (1.5 + 38.5 = 40). Non-walk trials keep 30 the same way.
        walkDuration = GetFloatArg("-walkDuration", walkTarget > 0.5f ? 38.5f : 30f);
        // FLAT SEQUENCE (cycle 96): the control plan (see ParseWalkPlan). Parsed here
        // because it falls back to squatReps / walkTarget; empty = off.
        walkPlanRaw = GetStringArg("-walkPlan", "");
        walkPlanBrake = GetFloatArg("-walkPlanBrake", 3.5f);    // CYCLE 123 landed: the stop brake armed by default; -walkPlanBrake 0 = off
        walkPlanBrakeFloor = GetFloatArg("-walkPlanBrakeFloor", 0.2f);
        walkPlanBrakeCatch = GetFloatArg("-walkPlanBrakeCatch", 0.9f);    // CYCLE 123 landed: the brake's catch share
        walkPlanBrakeFit = GetFloatArg("-walkPlanBrakeFit", 1f);    // CYCLE 124 landed: the ramp fits the desk (0 = the old mid-ramp cut)
        ParseWalkPlan();
        // DELEGATION req-272: player keys (default off). Parsed OUTSIDE the -level1
        // gate so a scene cmd can opt in without the level machinery.
        level1KeysFlag = GetFloatArg("-level1Keys", 0f) > 0.5f;
        level1KeysIdle = GetFloatArg("-level1KeysIdle", 0f);
        keyReplayPath = GetStringArg("-keyReplay", "");
        keyMaskScriptPath = GetStringArg("-keyMaskScript", "");
        keySquatBrakeWalk = GetFloatArg("-squatBrakeWalk", 0f) > 0.5f;
        keysOn = level1KeysFlag || keyReplayPath.Length > 0 || keyMaskScriptPath.Length > 0;
        if (keysOn)
        {
            // DELEGATION trial-keys: no seed here. With a -walkPlan SeedLivePlan was a
            // no-op (the plan is the intro); with none, the first key's CutPlanAt seeds
            // the live plan, so a take with no schedule (SampleScene) runs today's stand
            // path bit for bit until a key is pressed.
            OpenKeyLog();
            LoadKeyReplay();
            LoadKeyMaskScript();
            TrialRecorder.KeyColumns = true;   // req-272: keyDesk/keyMask appended LAST
        }
        walkStanceDuration = GetFloatArg("-walkStance", 6f);
        runTarget = Mathf.Clamp01(GetFloatArg("-run", 0f));
        runTime = GetFloatArg("-runTime", 5f);
        runHold = GetFloatArg("-runHold", 20f);
        runStanceDuration = GetFloatArg("-runStance", 4f);
        runTransferMaxDuration = GetFloatArg("-runTransfer", 4f);
        walkHeelStrikeMinAge = GetFloatArg("-walkHeelMin", 0f);
        walkHeelAirMin = GetFloatArg("-walkHeelAir", 0f);
        walkComTrigger = GetFloatArg("-walkComTrigger", 0.01f);
        walkComTriggerMinAge = GetFloatArg("-walkComTrigAge", 0.25f);
        walkComTriggerPerSpeed = GetFloatArg("-walkComTriggerPerSpeed", 0f);
        walkCadenceStanceGain = GetFloatArg("-walkCadenceStanceGain", 0f);
        walkCadenceMinStance = GetFloatArg("-walkCadenceMinStance", 1.0f);
        walkCadenceTransferGain = GetFloatArg("-walkCadenceTransferGain", 0f);
        walkCadenceMinTransfer = GetFloatArg("-walkCadenceMinTransfer", 0.5f);
        walkFirstStance = ParseStandLegArg(GetStringArg("-walkFirst", "right"));
        // DEMO WALK (27.09): the yard replica's own flags (see the fields' note).
        demoWalk = GetFloatArg("-demoWalk", 0f) > 0.5f;
        demoShift = GetFloatArg("-demoShift", 0f);
        demoSpawnX = GetFloatArg("-demoSpawnX", LevelTest1.PlayerX);
        // LEVEL 1 (req-217 step 2): the level forces the yard tape and its own spawn
        // (-8) with the human path; -level1Crows (default 20, the owner's 15.10 answer)
        // is the flock size. Absent for every other take, so those rows keep every byte.
        level1Wanted = GetFloatArg("-level1", 0f) > 0.5f;
        // req-237 Part 2: the level's own crowd life. Absent = old reads and bytes.
        level1CrowLife = level1Wanted && GetFloatArg("-level1CrowLife", 0f) > 0.5f;

        Level1LookFlags.Parse(GetFloatArg); // req-342: the look switches, default off
        // req-316: the soft yard band. Needs both level and the crowd life; ignored
        // otherwise, so a take without them keeps every old read and byte.
        level1YardBound = level1CrowLife && GetFloatArg("-level1YardBound", 0f) > 0.5f;
        level1YardMin = GetFloatArg("-level1YardMin", -14f);
        level1YardMax = GetFloatArg("-level1YardMax", 10f);

        // req-319: per-crow fear needs the crowd life; -level1CrowFearDog needs
        // the fear flag and the level dog. Ignored otherwise (byte-identical).
        level1CrowFear = level1CrowLife && GetFloatArg("-level1CrowFear", 0f) > 0.5f;
        level1CrowFearDog = level1CrowFear && GetFloatArg("-level1CrowFearDog", 0f) > 0.5f;

        // req-331: the fence-pad window and the own hop, under the life flag only.
        level1Perch = level1CrowLife && GetFloatArg("-level1Perch", 0f) > 0.5f;

        // req-246: the level's storyteller camera (state + shot rows), picture only.
        level1CamWanted = level1Wanted && GetFloatArg("-level1Cam", 0f) > 0.5f;
        level1CamSteady = level1CamWanted && GetFloatArg("-level1CamSteady", 0f) > 0.5f;
        // WORLD-1 (req-285): the living-world observer host, default off. Absent -world
        // creates no host, no brain, no System.Random and no file.
        worldWanted = level1Wanted && GetFloatArg("-world", 0f) > 0.5f;
        if (worldWanted)
        {
            worldSeedArg = HasArg("-worldSeed") ? Mathf.RoundToInt(GetFloatArg("-worldSeed", 1f)) : int.MinValue;
            worldLogLevel = Mathf.Clamp(Mathf.RoundToInt(GetFloatArg("-worldLog", 1f)), 0, 2);
            worldBenchWanted = GetFloatArg("-worldBench", 0f) > 0.5f;
        }
        if (level1Wanted)
        {
            demoWalk = true;
            demoSpawnX = GetFloatArg("-demoSpawnX", -8f);
        }
        // LEVEL1 DOG (req-231): the dog lives its own seeded life in the level.
        level1DogWanted = level1Wanted && GetFloatArg("-level1Dog", 0f) > 0.5f;
        demoKeepBehind = Mathf.RoundToInt(GetFloatArg("-demoKeepBehind", 2f));
        demoKeepAhead = Mathf.RoundToInt(GetFloatArg("-demoKeepAhead", 3f));
        walkTransferMaxDuration = GetFloatArg("-walkTransfer", 6f);
        walkTransferMinDuration = GetFloatArg("-walkTransferMin", 0.7f);
        walkTransferTouchdownMaxAge = GetFloatArg("-walkTransferTouchdownMaxAge", 1.2f);
        walkTransferComMax = GetFloatArg("-walkTransferCom", 0.10f);
        walkTransferFallbackComMax = GetFloatArg("-walkTransferFallbackCom", 0.12f);
        walkTransferLevelMax = GetFloatArg("-walkTransferLevel", 0.05f);
        // -useFallCatchWalk stays 0 unless Extra asks: WALK-2 bypass for
        // legacy -walk 1. Play AddComponent uses the driver field (1).
        // reachTimeout / fallAbortDeg / reachKneeFlex defaults must match
        // StepPhaseDriver — Play never runs this override.
        useFallCatchWalk = Mathf.RoundToInt(GetFloatArg("-useFallCatchWalk", 0f));
        fallComLead = GetFloatArg("-fallComLead", 0.025f);
        fallComLeadTrigger = GetFloatArg("-fallComLeadTrigger", 0.015f);
        fallPelvisLean = GetFloatArg("-fallPelvisLean", 12f);
        fallForwardLeanScale = GetFloatArg("-fallLeanScale", 0.75f);
        catchKneeFlex = GetFloatArg("-catchKneeFlex", 55f);
        // Capture-point Catch lead. -catchLeadCapture 0 restores the former
        // constant catchFootLead (0.08 m) in both the criterion and the
        // commanded step that is synced to it.
        catchLeadCapture = GetFloatArg("-catchLeadCapture", 1f);
        catchFootLeadMin = GetFloatArg("-catchFootLeadMin", 0.035f);
        catchLeadCaptureMargin = GetFloatArg("-catchLeadMargin", 0.015f);
        reachKneeFlex = GetFloatArg("-reachKneeFlex", 140f);
        fallStanceVault = GetFloatArg("-fallStanceVault", 1f);
        fallSwingThighHold = GetFloatArg("-fallSwingThighHold", 1f);
        fallSwingHipLift = GetFloatArg("-fallSwingHipLift", 0.25f);
        fallSwingAnkleLift = GetFloatArg("-fallSwingAnkleLift", 0.25f);
        fallSwingAirAnkle = GetFloatArg("-fallSwingAirAnkle", 1f);
        fallSwingAirHeel = GetFloatArg("-fallSwingAirHeel", 10f);
        fallSwingHeelPlant = GetFloatArg("-fallSwingHeelPlant", 1f);
        fallSwingScrapeExtend = GetFloatArg("-fallSwingScrapeExtend", 1f);
        fallSwingCatchLandKnee = GetFloatArg("-fallSwingCatchLandKnee", 1f);
        fallSwingPlantStanceKnee = GetFloatArg("-fallSwingPlantStanceKnee", 1f);
        fallSwingPlantMidStanceExtend = GetFloatArg("-walkPlantMidExtend", 1f);
        fallSwingVaultRamp = GetFloatArg("-fallSwingVaultRamp", 1f);
        fallSwingCatchVaultYield = GetFloatArg("-fallSwingCatchVaultYield", 1f);
        fallSwingArmClock = GetFloatArg("-fallSwingArmClock", 1f);
        fallSwingReachLiftClock = GetFloatArg("-fallSwingReachLiftClock", 1f);
        fallSwingReachArmClock = GetFloatArg("-fallSwingReachArmClock", 1f);
        fallSwingCatchArmClock = GetFloatArg("-fallSwingCatchArmClock", 1f);
        fallSwingCatchExtendClock = GetFloatArg("-fallSwingCatchExtendClock", 1f);
        fallSwingCatchReachResume = GetFloatArg("-fallSwingCatchReachResume", 1f);
        fallSwingCatchReachScrapeResume = GetFloatArg("-fallSwingCatchReachScrapeResume", 1f);
        fallSwingCatchReachScrapeLiftHold = GetFloatArg("-fallSwingCatchReachScrapeLiftHold", 1f);
        fallSwingCatchReachScrapeLiftAirAge = GetFloatArg("-fallSwingCatchReachScrapeLiftAirAge", 1f);
        fallSwingCatchReachScrapeVaultHold = GetFloatArg("-fallSwingCatchReachScrapeVaultHold", 1f);
        fallSwingCatchReachScrapeVaultAirAge = GetFloatArg("-fallSwingCatchReachScrapeVaultAirAge", 1f);
        fallSwingCatchReachScrapeArmHold = GetFloatArg("-fallSwingCatchReachScrapeArmHold", 1f);
        fallSwingCatchReachScrapeArmAirAge = GetFloatArg("-fallSwingCatchReachScrapeArmAirAge", 1f);
        fallSwingCatchReachScrapeBrakeHold = GetFloatArg("-fallSwingCatchReachScrapeBrakeHold", 1f);
        fallSwingCatchReachScrapeBrakeAirAge = GetFloatArg("-fallSwingCatchReachScrapeBrakeAirAge", 1f);
        fallSwingCatchReachScrapeYieldHold = GetFloatArg("-fallSwingCatchReachScrapeYieldHold", 1f);
        fallSwingCatchReachScrapeYieldAirAge = GetFloatArg("-fallSwingCatchReachScrapeYieldAirAge", 1f);
        fallSwingCatchReachScrapeExtendHold = GetFloatArg("-fallSwingCatchReachScrapeExtendHold", 1f);
        fallSwingCatchReachScrapeExtendAirAge = GetFloatArg("-fallSwingCatchReachScrapeExtendAirAge", 1f);
        fallSwingCatchReachVaultClock = GetFloatArg("-fallSwingCatchReachVaultClock", 1f);
        fallSwingCatchReachVaultAge = GetFloatArg("-fallSwingCatchReachVaultAge", 1f);
        fallSwingCatchReachArmClock = GetFloatArg("-fallSwingCatchReachArmClock", 1f);
        fallSwingCatchReachLiftClock = GetFloatArg("-fallSwingCatchReachLiftClock", 1f);
        fallSwingCatchReachLiftAge = GetFloatArg("-fallSwingCatchReachLiftAge", 1f);
        fallSwingCatchReachArmAge = GetFloatArg("-fallSwingCatchReachArmAge", 1f);
        fallSwingCatchAirVaultHold = GetFloatArg("-fallSwingCatchAirVaultHold", 1f);
        fallSwingCatchAirVaultRemain = GetFloatArg("-fallSwingCatchAirVaultRemain", 1f);
        fallSwingCatchAirArmHold = GetFloatArg("-fallSwingCatchAirArmHold", 1f);
        fallSwingCatchAirExtendHold = GetFloatArg("-fallSwingCatchAirExtendHold", 1f);
        fallSwingCatchScrapeExtendHold = GetFloatArg("-fallSwingCatchScrapeExtendHold", 1f);
        fallSwingCatchScrapeExtendAirAge = GetFloatArg("-fallSwingCatchScrapeExtendAirAge", 1f);
        fallSwingCatchScrapeExtendAirEnterHold = GetFloatArg("-fallSwingCatchScrapeExtendAirEnterHold", 1f);
        fallSwingCatchReachScrapeExtendEnterHold = GetFloatArg("-fallSwingCatchReachScrapeExtendEnterHold", 1f);
        fallSwingCatchAirLiftHold = GetFloatArg("-fallSwingCatchAirLiftHold", 1f);
        fallSwingCatchScrapeLiftAirHold = GetFloatArg("-fallSwingCatchScrapeLiftAirHold", 1f);
        fallSwingCatchScrapeLiftAirEnterHold = GetFloatArg("-fallSwingCatchScrapeLiftAirEnterHold", 1f);
        fallSwingCatchScrapeLiftAirCatchHold = GetFloatArg("-fallSwingCatchScrapeLiftAirCatchHold", 1f);
        fallSwingCatchScrapeLiftAirCatchFold = GetFloatArg("-fallSwingCatchScrapeLiftAirCatchFold", 1f);
        fallSwingCatchScrapeLiftAirEnterFold = GetFloatArg("-fallSwingCatchScrapeLiftAirEnterFold", 1f);
        fallSwingCatchScrapeLiftAirEnterArm = GetFloatArg("-fallSwingCatchScrapeLiftAirEnterArm", 1f);
        fallSwingCatchScrapeLiftAirCatchArm = GetFloatArg("-fallSwingCatchScrapeLiftAirCatchArm", 1f);
        fallSwingCatchScrapeLiftAirCatchEndArm = GetFloatArg("-fallSwingCatchScrapeLiftAirCatchEndArm", 1f);
        fallSwingCatchScrapeLiftAirCatchGroundArm = GetFloatArg("-fallSwingCatchScrapeLiftAirCatchGroundArm", 1f);
        fallSwingCatchScrapeLiftAirCatchAirArm = GetFloatArg("-fallSwingCatchScrapeLiftAirCatchAirArm", 1f);
        fallSwingCatchScrapeLiftAirCatchAirEndArm = GetFloatArg("-fallSwingCatchScrapeLiftAirCatchAirEndArm", 1f);
        fallSwingCatchScrapeLiftAirCatchAirGroundArm = GetFloatArg("-fallSwingCatchScrapeLiftAirCatchAirGroundArm", 1f);
        fallSwingCatchScrapeLiftAirEnterGroundArm = GetFloatArg("-fallSwingCatchScrapeLiftAirEnterGroundArm", 1f);
        fallSwingCatchScrapeLiftAirEnterGroundFold = GetFloatArg("-fallSwingCatchScrapeLiftAirEnterGroundFold", 1f);
        fallSwingCatchScrapeLiftAirCatchGroundFold = GetFloatArg("-fallSwingCatchScrapeLiftAirCatchGroundFold", 1f);
        fallSwingCatchScrapeLiftAirCatchAirGroundFold = GetFloatArg("-fallSwingCatchScrapeLiftAirCatchAirGroundFold", 1f);
        fallSwingCatchScrapeLiftAirCatchAirFold = GetFloatArg("-fallSwingCatchScrapeLiftAirCatchAirFold", 1f);
        fallSwingCatchScrapeLiftAirCatchAirEnterFold = GetFloatArg("-fallSwingCatchScrapeLiftAirCatchAirEnterFold", 1f);
        fallSwingCatchScrapeLiftAirCatchAirEnterArm = GetFloatArg("-fallSwingCatchScrapeLiftAirCatchAirEnterArm", 1f);
        fallSwingCatchScrapeLiftAirCatchAirEnterGroundArm = GetFloatArg("-fallSwingCatchScrapeLiftAirCatchAirEnterGroundArm", 1f);
        fallSwingCatchScrapeLiftAirCatchAirEnterGroundFold = GetFloatArg("-fallSwingCatchScrapeLiftAirCatchAirEnterGroundFold", 1f);
        fallSwingCatchScrapeLiftAirCatchAirEnterEndArm = GetFloatArg("-fallSwingCatchScrapeLiftAirCatchAirEnterEndArm", 1f);
        fallSwingCatchScrapeLiftAirCatchAirEnterCatchArm = GetFloatArg("-fallSwingCatchScrapeLiftAirCatchAirEnterCatchArm", 1f);
        fallSwingCatchScrapeLiftAirCatchAirEnterCatchFold = GetFloatArg("-fallSwingCatchScrapeLiftAirCatchAirEnterCatchFold", 1f);
        fallSwingCatchScrapeLiftAirCatchAirEnterEndFold = GetFloatArg("-fallSwingCatchScrapeLiftAirCatchAirEnterEndFold", 1f);
        fallSwingCatchScrapeLiftAirCatchEndFold = GetFloatArg("-fallSwingCatchScrapeLiftAirCatchEndFold", 1f);
        fallSwingCatchScrapeLiftAirCatchGroundCatchFold = GetFloatArg("-fallSwingCatchScrapeLiftAirCatchGroundCatchFold", 1f);
        fallSwingCatchScrapeLiftAirCatchGroundCatchEndFold = GetFloatArg("-fallSwingCatchScrapeLiftAirCatchGroundCatchEndFold", 1f);
        fallSwingCatchScrapeLiftAirCatchAirEndFold = GetFloatArg("-fallSwingCatchScrapeLiftAirCatchAirEndFold", 1f);
        fallSwingCatchScrapeLiftAirCatchAirGroundCatchFold = GetFloatArg("-fallSwingCatchScrapeLiftAirCatchAirGroundCatchFold", 1f);
        fallSwingCatchScrapeLiftAirCatchPlantExitFold = GetFloatArg("-fallSwingCatchScrapeLiftAirCatchPlantExitFold", 1f);
        fallSwingPlantStanceExtendClock = GetFloatArg("-fallSwingPlantStanceExtendClock", 1f);
        fallSwingVaultFromLeftover = GetFloatArg("-fallSwingVaultFromLeftover", 1f);
        fallSwingFallAnklePush = GetFloatArg("-fallSwingFallAnklePush", 1f);
        fallSwingFallHipLiftCap = GetFloatArg("-fallSwingFallHipLiftCap", 1f);
        fallSwingFallAnkleLift = GetFloatArg("-fallSwingFallAnkleLift", 1f);
        fallSwingFallHipLift = GetFloatArg("-fallSwingFallHipLift", 1f);
        fallSwingReachAirHold = GetFloatArg("-fallSwingReachAirHold", 1f);
        // Fall's own first air (23:39 can now produce it). 0 = former hipair jump.
        fallSwingFallAirHold = GetFloatArg("-fallSwingFallAirHold", 1f);
        // Fall 16° cap has room=0 once 23:47 keeps ThighHold there. 0 = former 16°.
        fallSwingFallHipLiftCapWide = GetFloatArg("-fallSwingFallHipLiftCapWide", 1f);
        // Reach 16° cap has room=0 once 23:53 walked Fall into 24°. 0 = former 16°.
        fallSwingReachHipLiftCapWide = GetFloatArg("-fallSwingReachHipLiftCapWide", 1f);
        // Catch-air 16° cap has room=0 once 23:58 walked Reach into 24°. 0 = former 16°.
        fallSwingCatchAirHipLiftCapWide = GetFloatArg("-fallSwingCatchAirHipLiftCapWide", 1f);
        // Plant 16° cap has room=0 once leftover + hipLift 0.5 sit there. 0 = former 16°.
        fallSwingPlantHipLiftCapWide = GetFloatArg("-fallSwingPlantHipLiftCapWide", 1f);
        // Fall 24° cap has room=0 once 00:09 walked Plant leftover into 24°. 0 = former 24°.
        fallSwingFallHipLiftCapCeil = GetFloatArg("-fallSwingFallHipLiftCapCeil", 1f);
        // Reach 24° cap has room=0 once 00:16 walked Fall into 32°. 0 = former 24°.
        fallSwingReachHipLiftCapCeil = GetFloatArg("-fallSwingReachHipLiftCapCeil", 1f);
        // Catch-air 24° cap has room=0 once 00:25 walked Reach into 32°. 0 = former 24°.
        fallSwingCatchAirHipLiftCapCeil = GetFloatArg("-fallSwingCatchAirHipLiftCapCeil", 1f);
        // Plant 24° cap has room=0 once leftover + hipLift 0.5 sit there. 0 = former 24°.
        fallSwingPlantHipLiftCapCeil = GetFloatArg("-fallSwingPlantHipLiftCapCeil", 1f);
        // Catch scrape 24° cap has room=0 once 00:28 walked Catch-air into 32°. 0 = former 24°.
        fallSwingCatchScrapeHipLiftCapCeil = GetFloatArg("-fallSwingCatchScrapeHipLiftCapCeil", 1f);
        // Catch→Reach scrape 24° cap has room=0 once 00:36 walked Catch scrape into 32°. 0 = former 24°.
        fallSwingCatchReachScrapeHipLiftCapCeil = GetFloatArg("-fallSwingCatchReachScrapeHipLiftCapCeil", 1f);
        // Reach→Catch timeout air still takes hipair −swingHipFlex. 0 = former latch jump.
        fallSwingCatchAirHold = GetFloatArg("-fallSwingCatchAirHold", 1f);
        // Catch-air keep still lost the hip on Catch→Reach bounce. 0 = former latch jump.
        fallSwingCatchAirEnterHold = GetFloatArg("-fallSwingCatchAirEnterHold", 1f);
        // Plant 32° fold still lost the hip on Plant's own first air. 0 = former latch jump.
        fallSwingPlantAirHold = GetFloatArg("-fallSwingPlantAirHold", 1f);
        // Plant-air leftover into Reach still ramped reachAirSwing to −swingHipFlex. 0 = former −40°.
        fallSwingReachAirSwingCeil = GetFloatArg("-fallSwingReachAirSwingCeil", 1f);
        // Recover lost-toe hipair still took −swingHipFlex (01:12 is Reach ramp only). 0 = former −40°.
        fallSwingHipairCeil = GetFloatArg("-fallSwingHipairCeil", 1f);
        // Recover still took the clamped pose (landing hip off). 0 = former 01:23 clamp.
        fallSwingRecoverHipairClear = GetFloatArg("-fallSwingRecoverHipairClear", 1f);
        // Recover-air still SPD latched ankle to joint=0 (foot ⊥ shin). 0 = former joint=0.
        fallSwingRecoverAirAnkleClear = GetFloatArg("-fallSwingRecoverAirAnkleClear", 1f);
        // Plant-air still SPD latched ankle to joint=0 (foot ⊥ shin). 0 = former joint=0.
        fallSwingPlantAirAnkleClear = GetFloatArg("-fallSwingPlantAirAnkleClear", 1f);
        // Plant-air drive world-flat instead of quiet / joint=0. 0 = former 01:45 skip.
        fallSwingPlantAirAnkleLevel = GetFloatArg("-fallSwingPlantAirAnkleLevel", 1f);
        // Recover-air still SPD swing knee to landDeg. 0 = former extend yank.
        fallSwingRecoverAirKneeClear = GetFloatArg("-fallSwingRecoverAirKneeClear", 1f);
        fallSwingReachHipLiftCap = GetFloatArg("-fallSwingReachHipLiftCap", 1f);
        fallSwingReachHipLift = GetFloatArg("-fallSwingReachHipLift", 1f);
        fallSwingReachAnkleLift = GetFloatArg("-fallSwingReachAnkleLift", 1f);
        fallSwingReachHipLiftCapHold = GetFloatArg("-fallSwingReachHipLiftCapHold", 1f);
        fallSwingFallHipLiftCapHold = GetFloatArg("-fallSwingFallHipLiftCapHold", 1f);
        fallSwingFallVaultLead = GetFloatArg("-fallSwingFallVaultLead", 1f);
        fallSwingFallLiftLead = GetFloatArg("-fallSwingFallLiftLead", 1f);
        fallSwingFallFoldLead = GetFloatArg("-fallSwingFallFoldLead", 1f);
        fallSwingCatchAirHipLiftCap = GetFloatArg("-fallSwingCatchAirHipLiftCap", 1f);
        fallSwingCatchAirHipLiftCapHold = GetFloatArg("-fallSwingCatchAirHipLiftCapHold", 1f);
        fallSwingCatchAirHipLift = GetFloatArg("-fallSwingCatchAirHipLift", 1f);
        fallSwingCatchAirAnkleLift = GetFloatArg("-fallSwingCatchAirAnkleLift", 1f);
        fallSwingCatchScrapeHipLiftCap = GetFloatArg("-fallSwingCatchScrapeHipLiftCap", 1f);
        fallSwingCatchScrapeHipLiftCapHold = GetFloatArg("-fallSwingCatchScrapeHipLiftCapHold", 1f);
        fallSwingCatchScrapeHipLift = GetFloatArg("-fallSwingCatchScrapeHipLift", 1f);
        fallSwingCatchScrapeAnkleLift = GetFloatArg("-fallSwingCatchScrapeAnkleLift", 1f);
        fallSwingCatchScrapeLiftLead = GetFloatArg("-fallSwingCatchScrapeLiftLead", 1f);
        fallSwingCatchScrapeHipLiftCapWide = GetFloatArg("-fallSwingCatchScrapeHipLiftCapWide", 1f);
        fallSwingCatchReachScrapeHipLiftCap = GetFloatArg("-fallSwingCatchReachScrapeHipLiftCap", 1f);
        fallSwingCatchReachScrapeHipLiftCapHold = GetFloatArg("-fallSwingCatchReachScrapeHipLiftCapHold", 1f);
        fallSwingCatchReachScrapeHipLift = GetFloatArg("-fallSwingCatchReachScrapeHipLift", 1f);
        fallSwingCatchReachScrapeAnkleLift = GetFloatArg("-fallSwingCatchReachScrapeAnkleLift", 1f);
        fallSwingCatchReachScrapeHipLiftCapWide = GetFloatArg("-fallSwingCatchReachScrapeHipLiftCapWide", 1f);
        plantMinDuration = GetFloatArg("-plantMinDuration", 0.50f);
        // Roll-over hold before a new Fall. -plantLeadWait 0 or
        // -fallComLeadTrigger 0 each restore the former release on
        // plantMinDuration alone.
        plantLeadWaitSeconds = GetFloatArg("-plantLeadWait", 1.0f);
        reachTimeout = GetFloatArg("-reachTimeout", 1.0f);
        fallAbortDeg = GetFloatArg("-fallAbortDeg", 30f);
        // Recovery ladder. Every one of these is inert on a healthy cycle;
        // 0 restores the former "no deadline" behavior for each rung
        // separately, so a regression can name the rung it blames.
        plantStallReseatSeconds = GetFloatArg("-plantStallReseat", 1.5f);
        plantReseatLeadMargin = GetFloatArg("-plantReseatLead", 0.05f);
        plantStallStandSeconds = GetFloatArg("-plantStallStand", 3f);
        // StandingRestore latched until Fall after a TiltSettled flicker, so
        // Plant push-off stayed off. 0 = former latch.
        plantClearRestoreOnGate = GetFloatArg("-plantClearRestoreOnGate", 1f);
        reachGroundStallSeconds = GetFloatArg("-reachGroundStall", 1.5f);
        fallCatchBounceMax = GetFloatArg("-fallCatchBounceMax", 4f);
        sinkAbortDrop = GetFloatArg("-sinkAbortDrop", 0.25f);
        plantAirLowestFoot = GetFloatArg("-plantAirLowestFoot", 1f);
        walkRecoverAnyPose = GetFloatArg("-walkRecoverAnyPose", 1f);
        if (Mathf.Abs(walkFirstStance) < 0.5f)
            walkFirstStance = 1f;

        // DROP TEST (line 3, 27.09): -drop <h> + -dropTime <s>. The release is a
        // pure rigid-body offset (see ReleaseDrop) scheduled on the trial clock;
        // -drop 0 is the default and leaves every other mode untouched.
        dropHeight = Mathf.Max(0f, GetFloatArg("-drop", 0f));
        dropTime = Mathf.Max(0.1f, GetFloatArg("-dropTime", dropTime));

        // TILT-PLATFORM STAND (29.09): -tiltPlatform 1 + the ramp's knobs
        // (-tiltPlatformLength, -tiltSettle, -tiltStepMove, -tiltStepHold,
        // -tiltFirstDeg, -tiltMaxDeg; the base is the shared -stanceWidth lever).
        // ROUND 16 (addendum 8): -tiltRow N / -tiltRowLen / -tiltRowGap / -tiltRowPhase
        // build the tilting bridge - a row of decks on one ramp clock.
        // The deck is spawned after BuildHuman (see BuildTiltStand): its top reads
        // the built rig's sole line. 0 = off.
        tiltWanted = GetFloatArg("-tiltPlatform", 0f) > 0.5f;
        // ROUND 13 (owner addendum 6: "если человек упал, нам необходимо отрабатывать
        // схему его подъёма"): -tiltGetUp 1 practices the rise on the stand; the man
        // presses his own get-up input the moment the deck puts him down and the pose
        // drive's ladder owns the rest (settle, attempt, the palms' press, retries).
        // -tiltGetUpHold 1 freezes the ramp at the fall. OFF by default.
        tiltGetUpWanted = GetFloatArg("-tiltGetUp", 0f) > 0.5f;
        tiltGetUpHoldWanted = GetFloatArg("-tiltGetUpHold", 0f) > 0.5f;
        // ROUND 35: the practice-deck policies (see the fields above).
        tiltGetUpHoldEaseDeg = Mathf.Max(0f, GetFloatArg("-tiltGetUpHoldEase", 0f));
        tiltVoidEndWanted = GetFloatArg("-tiltVoidEnd", 0f) > 0.5f;
        // ROUND 15 (the plant-phase ejection price/instrument): the deck's plate
        // thickness (0 = the drawn 0.06 m plate) and the palm-penetration trace.
        tiltThicknessArg = GetFloatArg("-tiltThickness", 0f);
        tiltPenWanted = GetFloatArg("-tiltPen", 0f) > 0.5f;
        // ROUND 40: the deck's freeze window (see TiltPlatform.freezeFromSeconds).
        tiltFreezeFromArg = GetFloatArg("-tiltFreezeFrom", -1f);
        tiltFreezeLenArg = Mathf.Max(0f, GetFloatArg("-tiltFreezeLen", 0f));

        string provenanceError;
        if (!TryAcceptProvenance(out runInfo, out provenanceError))
        {
            Debug.LogError("TRIAL_PROVENANCE_FAILED " + provenanceError);
            Application.Quit(3);
            return;
        }

        BuildTrialScene();

        // After the scene is built: the PhysX world already exists, so jobOptions must be pinned again.
        ConfigureDeterministicPhysics();

        StartCoroutine(RunTrial());
    }

    // ─── MINIMAL SCENE BUILD: GROUND + ONE HUMAN OR BIRD ───
    private void BuildTrialScene()
    {
        GameObject groundHost = new GameObject("TrialGround");
        GroundBuilder ground = groundHost.AddComponent<GroundBuilder>();
        bool wideBird = spawnBird && (birdDriveKind == "wander" || birdDriveKind == "prey" || birdDriveCount > 1);
        bool dogBirdScene = spawnDog && spawnBird;
        float packSpan = Mathf.Max(0, birdDriveCount - 1) * BirdPackSpacing;
        float birdGroundWidth = Mathf.Max(48f, packSpan + 16f);
        // DOG ARENA (cycle 64): deliberately NOT in the wide-arena branch. The dog's own scene keeps
        // the trial's own 20 m ground, because the arena's reference body has to be comparable with
        // the record row frame for frame — and a different ground collider re-clips the contact
        // manifold (measured: a 200 m ground diverges from the record row at f1765 by 1.3e-6 m of
        // chestY, the get-up's first loaded frame). The body's own travel is ~1 m in its 15 s
        // window, so 20 m is the field it needs.
        // THE RUNWAY (cycle 56). The floor under the walker IS the world's edge: the trial's
        // own ground is 20 m — 10 m of runway ahead of the spawn — and the accepted walk
        // reached it at 10.31 m and free-fell (c54: its recorded 29.51 m is 10.31 m of gait
        // plus 660 m of gravity). The runway beyond that edge is built by BuildRunwayFields
        // (NEW floor added, never a BIGGER old floor — measured: a 200 m box re-rounds the
        // contact clip's floats and moves recorded rows; see the method's comment).
        // The DOG SCENES KEEP 20 m ON PURPOSE (c64): the dog's own walk never reaches that edge
        // (measured max |x|: 2.2872 m in dog_c44_w04, 1.29 m in the c64 rows, 3.97 m in the dog
        // arena's best body) and the dog rows are byte-identical there (w56_dog_ctl == dog_c44_w04).
        // The bird/tree/apple/flower/impact trials likewise keep 20 m: no body of theirs ever
        // reaches |x| = 10 and their rows are kept artifacts.
        bool runwayWanted = !spawnArena && !wideBird && !dogBirdScene && !spawnDog
            && !spawnDogArena && !spawnImpact && !spawnBallHail && !spawnTree && !spawnApple && !spawnFlower
            && !spawnBird && !demoWalk && !tiltWanted;   // TILT STAND: the trial's own floor is skipped (see below)
        if (demoWalk)
        {
            // DEMO WALK (27.09): the yard tape IS the ground - no GroundBuilder
            // box, no runway (the yard scene has neither).
            BuildDemoTapeGround();
        }
        else if (tiltWanted)
        {
            // TILT STAND (29.09): the deck is the ONLY support, exactly as the
            // TiltStand scene builds it (GameProcess.noGround). The world floor cannot
            // stay: the deck top sits 2 mm above it, so on the downhill side of any
            // real tilt the floor - static, untilted - would carry the foot instead
            // of the deck and the stand's physics would lie.
        }
        else
        {
            ground.groundSize = new Vector2(spawnArena
                ? Mathf.Max(40f, arenaGroundMeters)
                : (wideBird || dogBirdScene ? birdGroundWidth : 20f), 2f);
            ground.groundPosition = new Vector2(0f, -3f);
            ground.groundFriction = GetFloatArg("-groundFriction", ground.groundFriction);
            ground.BuildGround();
        }

        // ARENA-1: ground and nothing else. The rig is not built here - the arena
        // spawns one human every 500 ms from ArenaRunner.
        if (spawnArena)
        {
            BuildArenaScene();
            return;
        }

        // DOG ARENA (cycle 64): ground and nothing else, exactly as the human arena's own scene -
        // the runner spawns the dogs. The ground is already built above.
        // THE ARENA'S RUNWAY (24.09, Yury's order: "Собаки доходят до края земли и падают вниз.
        // Необходимо увеличить размер земли."). The arena kept the trial's 20 m floor, and the
        // c64 note ("the dog arena's best body 3.97 m") held only until the dogs grew: today's
        // best walks past |x| = 10 and free-falls into the void in front of the owner. The same
        // cycle-56/70 idiom: NEW floor ADDED beyond the edge, never a BIGGER old floor (the old
        // box's contacts stay byte-identical), built before the runner spawns any dog.
        if (spawnDogArena)
        {
            if (Mathf.Abs(ground.groundSize.x - RunwayEdgeMeters * 2f) < 0.01f)
                BuildRunwayFields(ground.groundFriction);
            BuildDogArenaScene();
            return;
        }

        if (spawnDog)
        {
            BuildDogScene();
            if (spawnBird)
            {
                BuildBirdScene();
                WireDogBirdHunt();
            }
            // THE DOG'S OWN RUNWAY (cycle 70). The dog's trial keeps the trial's 20 m ground, but
            // 20 m IS the world's edge: the c68 layout row's front paw crossed x = 10.088 m at
            // t 17.39 s and the COM sank below the slab one contact later (the record row's own
            // deepest grounded paw reaches x = 2.293 m) - the fixture, not the gait, ended that
            // walk. This is the cycle-56 runway, the same idiom the walker's own path uses: NEW
            // floor ADDED beyond the edge, never a BIGGER old floor (a resized box re-rounds the
            // contact clip's floats and moves recorded rows), built LAST so every recorded object
            // keeps its creation order. The guard pins it to the 20 m trial box: the dog+bird
            // scene's own ground is birdGroundWidth (>= 48 m), where fields at |x| >= 10 m would
            // overlap the old box and double its contacts.
            if (Mathf.Abs(ground.groundSize.x - RunwayEdgeMeters * 2f) < 0.01f)
                BuildRunwayFields(ground.groundFriction);
            return;
        }

        if (spawnTree)
            BuildTreeScene();

        if (spawnApple)
            BuildAppleScene();

        if (spawnFlower)
            BuildFlowerScene();

        if (spawnBird)
        {
            BuildBirdScene();
            if (spawnTree && plantTree != null)
            {
                Bird[] pack = trialBirds != null && trialBirds.Length > 0
                    ? trialBirds
                    : bird != null ? new[] { bird } : System.Array.Empty<Bird>();
                Bird.GhostTreeWood(pack, plantTree);
            }
            if (spawnTree && treePerch && plantTree != null && bird != null)
                SeatTrialBirdsOnTree();
            return;
        }

        if (spawnTree || spawnFlower || spawnApple)
            return;

        GameObject humanObject = new GameObject("TrialHuman");
        // y = −0.82 puts the feet exactly on the top of the ground (−2.0):
        // (pelvisSize.y + torsoSize.y)/2 + thigh + shin + foot
        // = 0.25 + 0.43 + 0.43 + 0.07 = 1.18.
        // DEMO WALK (27.09): the yard replica spawns the walker at the yard
        // spawn (its tape is in yard coordinates).
        humanObject.transform.position = new Vector2(demoWalk ? demoSpawnX : 0f, startY);

        human = humanObject.AddComponent<Human>();
        human.footFriction = GetFloatArg("-footFriction", human.footFriction);
        human.footHeelShare = GetFloatArg("-footHeelShare", human.footHeelShare);
        // The hip arc bounds every hip target (ControlJointToAngleStable clamps
        // to joint.limits), so the split guard on the extension side is a
        // physics parameter the stand must be able to sweep without a rebuild.
        human.hipLimitMin = GetFloatArg("-hipLimitMin", human.hipLimitMin);
        human.hipLimitMax = GetFloatArg("-hipLimitMax", human.hipLimitMax);
        // CYCLE 88: THE STANCE-WIDTH LEVER (the owner's wider-legs idea, round 85 item 3).
        // -stanceWidth W is the anatomical hip separation in metres; the rig's own value
        // is 2 * hipHalfSpacing = 0.17. The view is near-profile (viewAngleDegrees 80),
        // so the lateral axis projects onto screen x by cos(80) = 0.1736 and the soles'
        // x separation is W * 0.1736 (the rig's own: 0.0295 m). Read BEFORE BuildHuman -
        // the leg hinges, shins and foot plates are placed from hipHalfSpacing - and a
        // take that passes nothing (or 0) gets the rig's own value, so every earlier
        // take, fence and KEEP row keeps its own geometry bit for bit.
        float stanceWidth = GetFloatArg("-stanceWidth", 0f);
        // DROP lane (28.09): a drop take stands on the wide base. Measured at +1.000 m
        // from a fence-clean stand: the rig's own 0.17 m base FAILS (after the first
        // contact the CoM creeps out of the base - the fall criterion trips at ~7.9 s),
        // while 0.26 / 0.34 / 0.36 / 0.40 m all HOLD, and the widest (0.40) also holds
        // under -2..+5 mm spawn jitter. The owner's stance lever (27.09) makes the
        // standing width a trial parameter; the drop mode takes the widest. An explicit
        // -stanceWidth always wins, and a non-drop take keeps the rig's own geometry.
        if (stanceWidth <= 0.001f && dropHeight > 0.01f)
            stanceWidth = 0.40f;
        if (stanceWidth > 0.001f)
            human.hipHalfSpacing = 0.5f * stanceWidth;
        // CYCLE 91: THE STRIDE STANCE (owner's 28.09 reference). -stanceStride s scales
        // the owner's split stand: the front ankle at +0.076*1.75 m and the back ankle
        // at -0.110*1.75 m from the pelvis (refs/owner-stance-20260928.md, H = the
        // rig's 1.75 m stature). The offsets are referred to the PELVIS, so the rig's
        // own lateral projection (hipHalfSpacing*cos80, already inside hipX) is
        // compensated: with s = 1 the ankles land exactly on the measured positions
        // and the ankle-to-ankle span is 0.186*1.75 = 0.3255 m. 0 (or absent) leaves
        // the rig's own geometry: every earlier take, fence and KEEP row moves no bit.
        float stanceStride = GetFloatArg("-stanceStride", 0f);
        if (stanceStride > 0.001f)
        {
            float lateral = human.hipHalfSpacing
                * Mathf.Cos(human.viewAngleDegrees * Mathf.Deg2Rad);
            human.stanceStrideFront = stanceStride * 0.076f * 1.75f - lateral;
            human.stanceStrideBack = stanceStride * 0.110f * 1.75f - lateral;
        }
        // MotionIntent and StepPhaseDriver before PlayerInputSource: that one
        // has RequireComponent(StepPhaseDriver), or Unity would create a second
        // instance and the stand would Tick “ours” while metrics read the other.
        intent = humanObject.AddComponent<MotionIntent>();
        stepDriver = humanObject.AddComponent<StepPhaseDriver>();
        stepDriver.externalDrive = true;
        stepDriver.stanceDuration = walkStanceDuration;
        stepDriver.stanceHeelStrikeMinAge = walkHeelStrikeMinAge;
        stepDriver.stanceHeelAirMin = walkHeelAirMin;
        stepDriver.stanceComTrigger = walkComTrigger;
        stepDriver.stanceComMinAge = walkComTriggerMinAge;
        stepDriver.stanceComTriggerPerSpeed = walkComTriggerPerSpeed;
        stepDriver.walkComTriggerMinStanceFraction = GetFloatArg("-walkComTriggerMinStanceFraction", stepDriver.walkComTriggerMinStanceFraction);
        stepDriver.walkTiltAbortDeg = GetFloatArg("-walkTiltAbort", stepDriver.walkTiltAbortDeg);
        stepDriver.walkTiltRecoverDeg = GetFloatArg("-walkTiltRecover", stepDriver.walkTiltRecoverDeg);
        stepDriver.walkCadenceStanceGain = walkCadenceStanceGain;
        stepDriver.walkCadenceMinStance = walkCadenceMinStance;
        stepDriver.transferMaxDuration = walkTransferMaxDuration;
        stepDriver.transferMinDuration = walkTransferMinDuration;
        stepDriver.transferTouchdownMaxAge = walkTransferTouchdownMaxAge;
        stepDriver.walkCadenceTransferGain = walkCadenceTransferGain;
        stepDriver.walkCadenceMinTransfer = walkCadenceMinTransfer;
        stepDriver.transferComOffsetMax = walkTransferComMax;
        stepDriver.transferFallbackComMax = walkTransferFallbackComMax;
        stepDriver.transferStandLegLevelMax = walkTransferLevelMax;
        stepDriver.firstStance = walkFirstStance;
        ApplyFallCatchWalkFields(stepDriver);
        humanObject.AddComponent<PlayerInputSource>();

        // Torques and friction are read when the body is built, so change them before BuildHuman.
        float muscleMultiplier = GetFloatArg("-muscle", 1f);
        float frictionMultiplier = GetFloatArg("-friction", 1f);
        ApplyMultipliers(human, muscleMultiplier, frictionMultiplier);
        // Absolute K after the multiplier: a diagnostic tau sweep
        // must not depend on -friction and must not touch Human defaults.
        ApplyFrictionOverrides(human);
        ApplyMuscleOverrides(human);

        human.BuildHuman();
        FactionStamp.Player(humanObject);

        // TILT STAND (29.09, the owner's stand): the deck under the standing man,
        // built AFTER BuildHuman - its top reads the built rig's sole line
        // (TiltPlatform.LowestContactY), exactly as GameProcess does for the
        // TiltStand scene. Armed only by -tiltPlatform 1; every other take is
        // untouched.
        if (tiltWanted)
            BuildTiltStand(humanObject);

        // ART-H2 (art lane, view only): one flag dresses the mannequin with the painted
        // canvases after the rig exists. HumanArt writes only the segment renderer
        // (draw mode, sprite, size), so the walk row must not move with it. Logged, so a
        // film's own log carries the dressed count and any missing canvas.
        // ART-H3 (art lane): same flag, same contract - the ART-H3 canvases only soften the
        // joints' seams; the row must stay bit-equal to the accepted one.
        if (GetFloatArg("-dressHuman", 0f) > 0.5f)
        {
            string firstMissing;
            int missing = HumanArt.MissingCanvases(out firstMissing);
            int dressed = HumanArt.DressHuman(human.transform, true);
            Debug.Log("ART-H2 dressHuman dressed=" + dressed + " missing=" + missing
                + (missing > 0 ? " first=" + firstMissing : ""));
        }

        // Controller gains live in BalanceController, which appears
        // inside BuildHuman, so override them after the build.
        BalanceController balance = human.GetComponent<BalanceController>();
        ApplyGains(balance);
        // The driver is already on the object before Awake. Bind closes a late-add
        // in one call, without GetComponent in FixedUpdate.
        if (balance != null)
            balance.BindStepPhaseDriver(stepDriver);
        // CYCLE 97b: the stop brake's catch seat lives on the pose controller
        // (ReflexCatchShare - see the brake block in UpdateWalkIntent).
        poseDrive = human.GetComponent<PoseDriveController>();

        // WALK REFLEX (27.09): armed only by its own flag, so every trial row
        // that does not pass -walkReflex 1 stays bit for bit the landed one -
        // the component is the only writer of the pose controller's Reflex*
        // fields (Assets/WalkReflex.cs). The demo replica and the evolution
        // batteries pass it.
        if (GetFloatArg("-walkReflex", 0f) > 0.5f)
            humanObject.AddComponent<WalkReflex>();

        // THE FALL REFLEX (29.09, owner's addendum: "по умолчанию должна быть защитная
        // реакция"): the protective layer below the recovery set (arms forward, one leg
        // steps, head kept off the ground). Default ARMED - it is inert without a fire
        // (no fall, no bit), so stands, anchors and fences keep their rows; -fallReflex 0
        // disarms it for an A/B. Assets/FallReflex.cs owns its knobs and its discipline.
        if (GetFloatArg("-fallReflex", 1f) > 0.5f)
            humanObject.AddComponent<FallReflex>();

        // THE INTUITIVE STANCE (29.09, the owner's addendum 2: "человек сам должен
        // интуитивно менять стойку в зависимости от окружающих факторов"): on the moving
        // deck the man manages his own base - he steps his stance wider as the tilt grows
        // and back in when it is calm (Assets/IntuitiveStance.cs). Armed only by its own
        // flag, so every row that does not pass -intuitiveStance 1 (and every non-tilt
        // take) stays bit for bit.
        if (tiltWanted && GetFloatArg("-intuitiveStance", 0f) > 0.5f)
        {
            IntuitiveStance stance = humanObject.AddComponent<IntuitiveStance>();
            stance.platform = tiltStand;
        }

        // THE TILT LEAN (29.09 round 6, the owner's program: "прогибаться вперёд/назад,
        // сгибать ноги"): while the deck rocks the man rides it - a graded knee fold
        // (intent.crouch) and a compensatory trunk lean away from the dropping end
        // (intent.lean), both through the core's own machinery (Assets/TiltLean.cs).
        // Added only for -tiltPlatform takes and only when armed, so every other take -
        // and the disarmed control (-tiltLean 0) - stays bit for bit.
        if (tiltWanted && GetFloatArg("-tiltLean", 1f) > 0.5f)
        {
            TiltLean ride = humanObject.AddComponent<TiltLean>();
            ride.platform = tiltStand;
            tiltRide = ride;
        }

        // THE BRACE REFLEX (29.09 round 7, the owner's addendum 5: "Если близко к голове
        // приближается, на вытянутых руках, какая-то поверхность, то человек выставляет
        // руки на нее"): a surface inside arm's reach of the head/chest gets the hands -
        // they reach it and push (Assets/BraceReflex.cs). Added only for -tiltPlatform
        // takes and only when armed (default 1), so every other take - and the disarmed
        // control (-braceReflex 0) - stays bit for bit.
        if (tiltWanted && GetFloatArg("-braceReflex", 1f) > 0.5f)
            humanObject.AddComponent<BraceReflex>();

        // THE SELF-ORIENTATION CORE (29.09 round 17, the owner's addendum 8: "нам нужно
        // создать логику анализа своего положения человека относительно окружающего
        // мира, это основа нашей физики человека"). One pose state, read by the fall
        // reflex and the arm press so a body on its back is never driven into the
        // face-down reach. c102: ARMED BY DEFAULT FOR FLAT GET-UP TAKES - the
        // get-up ladder's own back-branch (the roll re-entry, the roll stage,
        // the lying-back arm rest) reads this core; without it a flat get-up
        // take is blind to a supine body (measured on c101land_a: the somersault
        // at 22.5 s, then 67 s of the prone reach into a back-lying body, zero
        // back-aware frames). -poseOrient 1 forces it on everywhere; 0 forces it
        // off (the pre-c102 take bit for bit); tilt takes keep their own
        // explicit arming (guarded off here).
        float orientArg = GetFloatArg("-poseOrient", -1f);
        bool orientWanted = orientArg > 0.5f
            || (orientArg < 0f && !tiltWanted && GetFloatArg("-poseGetUp", 0f) > 0.5f);
        if (orientWanted)
            humanObject.AddComponent<PoseOrientation>();

        Transform torso = human.transform.Find("Torso");
        torsoBody = torso != null ? torso.GetComponent<Rigidbody2D>() : null;

        if (spawnImpact)
            BuildImpactScene();
        if (spawnBallHail)
            BuildBallScene();

        // LEVEL 1 (req-217 step 2): the tableau of the level on the yard tape - the
        // drawn grove, the campfire and the crow flock (the decor marks ride with the
        // chunks, see DemoStreamChunks). Only under -level1; every other take is bit for bit.
        if (level1Wanted)
            BuildLevel1Tableau();

        // THE RUNWAY (cycle 56): the floor beyond the trial's own 20 m ground. Built LAST so
        // every object of the recorded scene keeps its creation order (the road to bit-identity).
        if (runwayWanted)
            BuildRunwayFields(ground.groundFriction);
    }

    // TILT STAND (29.09, the owner's stand): build the deck under the standing man.
    // Called right after BuildHuman, because the deck's top is read from the BUILT
    // rig (LowestContactY: root - 1.18 m = the sole line the deck must meet); the
    // world floor is not built for this take at all (see the ground gate above).
    // The knobs are the ones the owner's drawing named: length 3 m, the central
    // pivot, the imposed ramp. Defaults (no flags) are the drawn stand.
    private void BuildTiltStand(GameObject humanObject)
    {
        float length = GetFloatArg("-tiltPlatformLength", 5f);
        tiltStand = TiltPlatform.Spawn(
            humanObject.transform.position.x,
            TiltPlatform.LowestContactY(humanObject),
            length,
            tiltThicknessArg);
        tiltStand.settleSeconds = GetFloatArg("-tiltSettle", tiltStand.settleSeconds);
        tiltStand.stepMoveSeconds = GetFloatArg("-tiltStepMove", tiltStand.stepMoveSeconds);
        tiltStand.stepHoldSeconds = GetFloatArg("-tiltStepHold", tiltStand.stepHoldSeconds);
        tiltStand.firstStepDeg = GetFloatArg("-tiltFirstDeg", tiltStand.firstStepDeg);
        tiltStand.maxStepDeg = GetFloatArg("-tiltMaxDeg", tiltStand.maxStepDeg);
        tiltStand.stepIncrementDeg = GetFloatArg("-tiltStepDeg", tiltStand.stepIncrementDeg);
        tiltStand.rampFrozenEaseDegPerSec = tiltGetUpHoldEaseDeg;   // ROUND 35: the level-ease hold
        tiltStand.freezeFromSeconds = tiltFreezeFromArg;            // ROUND 40: the freeze window
        tiltStand.freezeLenSeconds = tiltFreezeLenArg;

        // ROUND 16 (the owner's addendum 8: "добавь уже несколько платформ", the tilting
        // bridge): -tiltRow N > 1 builds a ROW of N decks in a line along the walk axis,
        // deck k centred k*(len+gap) ahead of deck 0, on the SAME ramp clock - the row
        // rocks "в такт". -tiltRowPhase shifts deck k by k*phase seconds. N = 1 (default)
        // spawns NOTHING extra: every pre-existing tilt take keeps its bytes.
        int rowDecks = Mathf.RoundToInt(GetFloatArg("-tiltRow", 1f));
        float rowLen = GetFloatArg("-tiltRowLen", length);
        float rowGap = GetFloatArg("-tiltRowGap", 0.10f);
        float rowPhase = GetFloatArg("-tiltRowPhase", 0f);
        if (rowDecks > 1)
        {
            for (int k = 1; k < rowDecks; k++)
            {
                TiltPlatform rowDeck = TiltPlatform.Spawn(
                    humanObject.transform.position.x + k * (rowLen + rowGap),
                    TiltPlatform.LowestContactY(humanObject),
                    rowLen,
                    tiltThicknessArg);
                rowDeck.settleSeconds = tiltStand.settleSeconds;
                rowDeck.stepMoveSeconds = tiltStand.stepMoveSeconds;
                rowDeck.stepHoldSeconds = tiltStand.stepHoldSeconds;
                rowDeck.firstStepDeg = tiltStand.firstStepDeg;
                rowDeck.maxStepDeg = tiltStand.maxStepDeg;
                rowDeck.stepIncrementDeg = tiltStand.stepIncrementDeg;
                rowDeck.phaseOffsetSeconds = k * rowPhase;
            }
        }
        Debug.Log("TILT-STAND built: deck=" + length.ToString("0.###", CultureInfo.InvariantCulture)
            + " m top=" + TiltPlatform.LowestContactY(humanObject).ToString("0.####", CultureInfo.InvariantCulture)
            + " settle=" + tiltStand.settleSeconds.ToString("0.###", CultureInfo.InvariantCulture)
            + " move=" + tiltStand.stepMoveSeconds.ToString("0.###", CultureInfo.InvariantCulture)
            + " hold=" + tiltStand.stepHoldSeconds.ToString("0.###", CultureInfo.InvariantCulture)
            + " first=" + tiltStand.firstStepDeg.ToString("0.###", CultureInfo.InvariantCulture)
            + " max=" + tiltStand.maxStepDeg.ToString("0.###", CultureInfo.InvariantCulture));
        if (rowDecks > 1)
            Debug.Log("TILT-STAND ROW built: decks=" + rowDecks
                + " len=" + rowLen.ToString("0.###", CultureInfo.InvariantCulture)
                + " gap=" + rowGap.ToString("0.###", CultureInfo.InvariantCulture)
                + " phase=" + rowPhase.ToString("0.###", CultureInfo.InvariantCulture));
    }

    // ─── THE RUNWAY (cycle 56) ───
    // The trial's floor is 20 m — 10 m of runway ahead of the spawn — and it IS the world's edge
    // (Yury 23.09: "the ground is too small ... the people already reach the edge"; c54's accepted
    // walk crossed x = +10 at 28.06 s and free-fell 660 m). The runway is NEW floor ADDED beyond
    // that edge, never a BIGGER old floor: a resized ground box re-rounds the contact clip's floats
    // and moves recorded rows. Measured on a 200 m build, 23.09 10:32 (buildGuid feca3719): the #92
    // row became f2603b2e (vs the record 3f5625b8) with the first difference at frame 3319
    // (t 16.600 s — the POST-FALL phase; the gait itself and the whole stance a7e18eb9 stayed
    // byte-identical). With the old box untouched, every body inside |x| <= 10 m keeps its exact
    // contact math — the fence the order demands — and beyond it there is now ground.
    private const float RunwayEdgeMeters = 10f;    // the old 20 m ground's own half width
    private const float RunwayHalfMeters = 100f;   // the house scale: the arena's 200 m default

    // Two dirt fields beyond the edge, built exactly as GroundBuilder.BuildGround builds its own
    // ground (same layer, same material, same tiled slab, same grass strip), so the world reads
    // as one floor and the trial's ground keeps its name ("Ground" — ArmDrive/BodyStateEstimator
    // look it up) alone.
    private void BuildRunwayFields(float friction)
    {
        for (int side = -1; side <= 1; side += 2)
        {
            float fromX = side > 0 ? RunwayEdgeMeters : -RunwayHalfMeters;
            float toX = side > 0 ? RunwayHalfMeters : -RunwayEdgeMeters;
            float width = toX - fromX;
            if (width <= 0.01f)
                continue;
            GameObject host = new GameObject(side > 0 ? "TrialRunwayE" : "TrialRunwayW");
            GroundBuilder field = host.AddComponent<GroundBuilder>();
            field.groundSize = new Vector2(width, 2f);
            field.groundPosition = new Vector2((fromX + toX) * 0.5f, -3f);
            field.groundFriction = friction;
            field.BuildGround();
        }
    }

    // ─── DEMO WALK (27.09, task "Evolutionary demo walk") ───
    // The headless twin of the public demo's walkway: the yard tape from
    // LevelTest1 (the same pieces LevelGenerator.BuildChunk spawns in the
    // World / Level1 scenes), streamed in 16 m chunks around the walker.
    private void BuildDemoTapeGround()
    {
        demoLevelRoot = new GameObject("Level").transform;
        DemoStreamChunks(demoSpawnX, true);
    }

    // LevelGenerator.StreamAround's own rule: center = floor(yardX / chunkWidth),
    // min = center - keepBehind, max = center + keepAhead, out-of-range chunks
    // destroyed first, missing built in index order. The chunk pieces come from
    // LevelTest1.PiecesForChunk (the shipped tape), shifted by -demoShift.
    private void DemoStreamChunks(float humanX, bool force)
    {
        float yardX = humanX - demoShift;
        int center = Mathf.FloorToInt(yardX / 16f);
        if (!force && center == demoLastCenter)
            return;
        demoLastCenter = center;

        int min = center - demoKeepBehind;
        int max = center + demoKeepAhead;

        demoDrop.Clear();
        foreach (var pair in demoChunks)
        {
            if (pair.Key < min || pair.Key > max)
                demoDrop.Add(pair.Key);
        }
        for (int i = 0; i < demoDrop.Count; i++)
        {
            int index = demoDrop[i];
            GameObject chunk;
            if (demoChunks.TryGetValue(index, out chunk) && chunk != null)
                Destroy(chunk);
            demoChunks.Remove(index);
        }

        for (int index = min; index <= max; index++)
        {
            if (demoChunks.ContainsKey(index))
                continue;
            GameObject chunk = new GameObject("Chunk_" + index);
            chunk.transform.SetParent(demoLevelRoot, false);
            List<LevelPiece> pieces = LevelTest1.PiecesForChunk(index, 16f);
            for (int p = 0; p < pieces.Count; p++)
            {
                LevelPiece piece = pieces[p];
                piece.center += new Vector2(demoShift, 0f);
                StaticPlatform.Spawn(piece, chunk.transform);
            }
            if (level1Wanted)
            {
                // LEVEL 1 (req-217 step 2): the tape's decor marks (fence posts,
                // clothesline, lamp post) - picture only, no colliders - exactly
                // LevelGenerator.BuildChunk's own call.
                try
                {
                    LevelTest1.SpawnDecor(index, 16f, chunk.transform);
                }
                catch (System.Exception e)
                {
                    Debug.LogException(e);
                }
            }
            demoChunks[index] = chunk;
        }
    }

    // ─── LEVEL 1 (req-217 step 2) ───
    // The tableau of the level, built ON the demo tape with the walker: the drawn
    // grove (LevelGrove = the Level1.unity content), the campfire slid into the shot,
    // the hedgehog moved OFF the walk path (the constant stays for Editor Play) and
    // the crow flock by GameProcess's own recipe (CreateBirds + SeatBirdsOnYardGrove
    // + DisableCollisionsWithBirds), with the flock brain armed for the headless clock.
    // Daylight holds by construction: the trial never creates a DayNightCycle.
    private void BuildLevel1Tableau()
    {
        // The picture props first, each step fenced (breadcrumbs in the log).
        try
        {
            LevelGrove.Plant(demoLevelRoot, human != null ? human.transform : null, true);
            Debug.Log("LEVEL1 grove ok planted=" + (LevelGrove.Planted != null ? LevelGrove.Planted.Length : 0));
            gameObject.AddComponent<Level1SortAudit>(); // req-342 Do 7: the LOOK sort line
        }
        catch (System.Exception e)
        {
            Debug.LogError("LEVEL1 grove failed: " + e);
        }

        try
        {
            Campfire fire = Campfire.EnsureOnYard();
            if (fire != null)
                LevelGrove.PlaceDrawnCampfire(fire);
            Debug.Log("LEVEL1 campfire ok");
        }
        catch (System.Exception e)
        {
            Debug.LogError("LEVEL1 campfire failed: " + e);
        }

        // The hedgehog's drawn spot (LevelGrove.HedgehogX, -6.4) is ON the plan's path;
        // the level slides THIS instance to -11. The constant is not touched, so the
        // Editor Play of Level1 keeps its drawn tableau.
        try
        {
            GameObject hedgehog = GameObject.Find("YardHedgehog");
            if (hedgehog != null)
            {
                float hedgeY = LevelTest1.SurfaceAt(Level1HedgehogX);
                hedgehog.transform.position = new Vector2(Level1HedgehogX, Hedgehog.StandingRootY(hedgeY));
                Debug.Log("LEVEL1 hedgehog moved to " + Level1HedgehogX);
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError("LEVEL1 hedgehog failed: " + e);
        }

        int count = Mathf.Max(1, Mathf.RoundToInt(GetFloatArg("-level1Crows", 20f)));
        float originX = LevelGrove.Apple != null ? LevelGrove.Apple.transform.position.x : 0f;
        level1Crows = new Bird[count];
        level1CrowBodies = new Rigidbody2D[count];
        for (int i = 0; i < count; i++)
        {
            // One crow's build is fenced on its own: a single bad body must not cost
            // the level its flock (the failure and its index land in the log).
            try
            {
                GameObject go = new GameObject("Level1Crow_" + (i + 1));
                float x = originX + i * 0.35f;
                float crowY = Bird.KindStandingRootY(BirdKind.Crow, LevelTest1.SurfaceAt(x));
                go.transform.position = new Vector2(x, crowY);
                Bird crow = Bird.Attach(go, BirdKind.Crow, BirdRig.Flock);
                if (crow == null)
                {
                    Debug.LogError("LEVEL1 crow " + i + ": Attach returned null");
                    continue;
                }
                FactionStamp.Bird(go);
                crow.BuildBird();
                if (crow.controller != null)
                {
                    crow.controller.flapPhaseOffset = i * 0.13f;
                    crow.controller.takeoffDelay = 0f;
                    crow.controller.turnAfter = 0f;
                    crow.controller.mode = BirdMode.Sit;
                }
                level1Crows[i] = crow;
                Transform crowBody = go.transform.Find("Body");
                level1CrowBodies[i] = crowBody != null ? crowBody.GetComponent<Rigidbody2D>() : null;
            }
            catch (System.Exception e)
            {
                Debug.LogError("LEVEL1 crow " + i + " failed: " + e);
            }
        }
        Debug.Log("LEVEL1 crows built=" + CountLevel1Crows());

        // The hero is read from its own RIGIDBODY: autoSyncTransforms is off, so the
        // host transform never leaves the spawn (the recorder reads bodies for the same reason).
        Transform pelvis = human != null ? human.transform.Find("Pelvis") : null;
        level1HeroBody = pelvis != null ? pelvis.GetComponent<Rigidbody2D>() : null;

        GameObject host = new GameObject("Level1Flock");
        BirdFlockDrive flock = host.AddComponent<BirdFlockDrive>();
        flock.allowHeadless = true;
        // req-237 Part 2: inside -level1CrowLife the flock seed follows the take
        // when -level1Seed is absent, so every take's crowd differs (Part 1: the
        // fixed seed 1 drew the same takeoff vector in 4 of 4 takes).
        int level1FlockSeed = 1;
        if (HasArg("-level1Seed"))
            level1FlockSeed = Mathf.RoundToInt(GetFloatArg("-level1Seed", 1f));
        else if (level1CrowLife)
        {
            int h = 17;
            for (int i = 0; i < label.Length; i++)
                h = (h * 31 + label[i]) % 1000003;
            level1FlockSeed = h + 1;
        }
        flock.seed = level1FlockSeed;
        // req-237 Part 2: the level-scoped crowd life. The flock reads the hero
        // PELVIS body, flushes as one pack off the crown and resettles to the
        // sites (fence rail's west stretch, lamp top, dirt) - all out of the
        // hero's release radius once he stands under the crown (x ~ 1.08).
        flock.levelCrowLife = level1CrowLife;
        // req-316: the soft yard band rides the same level switch; off it never runs.
        flock.levelYardBound = level1YardBound;
        flock.levelYardMin = level1YardMin;
        flock.levelYardMax = level1YardMax;
        if (level1YardBound)
            Debug.Log("LEVEL1 YARD band min=" + level1YardMin.ToString("0.##", CultureInfo.InvariantCulture)
                + " max=" + level1YardMax.ToString("0.##", CultureInfo.InvariantCulture));
        flock.levelPerch = level1Perch;
        flock.levelHeroBody = level1CrowLife ? level1HeroBody : null;
        flock.levelCrowFear = level1CrowFear;
        flock.levelCrowFearDog = level1CrowFearDog;
        if (level1CrowLife)
        {
            flock.SetLevelSites(Level1SiteSlots(), Level1SiteKinds());
            Level1BuildSitePads();
        }
        flock.Bind(level1Crows, flock.seed);
        flock.threat = human != null ? human.transform : null;
        trialBirds = level1Crows;
        trialFlockDrive = flock;
        level1FlockDrive = flock;
        bird = level1Crows[0];

        // GameProcess.SeatBirdsOnYardGrove: the ghost wood and the apple's pads, so the
        // flock starts on the tree the way the scene does it.
        if (LevelGrove.Planted != null && LevelGrove.Planted.Length > 0)
        {
            Bird.GhostTreeWood(level1Crows, LevelGrove.Planted);
            PlantTree perch = LevelGrove.Apple != null ? LevelGrove.Apple : LevelGrove.Planted[0];
            Vector2[] slots = perch.GetPerchSlots();
            flock.BindPerches(slots, perch.PerchPadCount);
            Debug.Log("LEVEL1 perches slots=" + (slots != null ? slots.Length : -1));
        }
        Debug.Log("LEVEL1 flock members=" + flock.MemberCount);

        // GameProcess.DisableCollisionsWithBirds: human-bird and bird-bird pairs are
        // ignored, so the flock passes through the hero (the judge's contact check).
        Collider2D[] hero = human != null ? human.GetComponentsInChildren<Collider2D>() : new Collider2D[0];
        Collider2D[][] crowCols = new Collider2D[level1Crows.Length][];
        for (int i = 0; i < level1Crows.Length; i++)
        {
            crowCols[i] = level1Crows[i] != null
                ? level1Crows[i].GetComponentsInChildren<Collider2D>()
                : new Collider2D[0];
            for (int a = 0; a < hero.Length; a++)
                for (int b = 0; b < crowCols[i].Length; b++)
                    Physics2D.IgnoreCollision(hero[a], crowCols[i][b], true);
        }
        for (int i = 0; i < crowCols.Length; i++)
            for (int j = i + 1; j < crowCols.Length; j++)
                for (int a = 0; a < crowCols[i].Length; a++)
                    for (int b = 0; b < crowCols[j].Length; b++)
                        Physics2D.IgnoreCollision(crowCols[i][a], crowCols[j][b], true);

        if (level1DogWanted)
            BuildLevel1Dog();
        // req-319: the dog's chest body is the second threat (same source as
        // Level1DogChestPoint). Null unless -level1CrowFearDog 1.
        if (level1CrowFearDog && level1FlockDrive != null && level1Dog != null
            && level1Dog.chestSegment != null)
            level1FlockDrive.levelDogBody = level1Dog.chestSegment.rb;

        // req-246: the storyteller camera is armed with the level (reads only).
        if (level1CamWanted)
        {
            level1AppleTree = LevelGrove.Apple;
            level1StoryCam = new Level1StoryCam();
            Debug.Log("LEVEL1 storycam armed apple=" + (level1AppleTree != null ? 1 : 0));
        }

        // WORLD-1 (req-285): the observer host is the LAST object built, so no
        // instance id created before it moves (the dog seeds from GetHashCode()).
        if (worldWanted)
        {
            int worldSeed = worldSeedArg != int.MinValue ? worldSeedArg
                : (HasArg("-level1Seed") ? Mathf.RoundToInt(GetFloatArg("-level1Seed", 1f)) : 1);
            BalanceController worldBalance = human != null ? human.GetComponent<BalanceController>() : null;
            GameObject worldObject = new GameObject("Level1World");
            worldHost = worldObject.AddComponent<WorldBrainHost>();
            worldHost.Configure(worldSeed, worldLogLevel, worldBenchWanted, outputFolder, label,
                level1HeroBody, worldBalance, stepDriver, intent, level1Dog, level1Crows, level1CrowBodies);
            Debug.Log("WORLD armed seed=" + worldSeed + " log=" + worldLogLevel
                      + " bench=" + (worldBenchWanted ? 1 : 0) + " hero=" + (level1HeroBody != null ? 1 : 0));
        }
    }

    // LEVEL1 DOG (req-231): one Dog at hero x - 1.5, brain armed, the S13 pose line.
    // Its colliders never touch the hero's, so the hero rows stay byte for byte.
    private void BuildLevel1Dog()
    {
        GameObject dogObject = new GameObject("Level1Dog");
        level1Dog = dogObject.AddComponent<Dog>();
        ApplyDogBuildOverrides(level1Dog);
        // The accepted dog walk line's own paw grip (-dogPawFriction 0.4, S13).
        level1Dog.pawContactFriction = GetFloatArg("-dogPawFriction", 0.4f);
        float dogX = demoSpawnX - 1.5f;
        float groundY = LevelTest1.SurfaceAt(dogX);
        float rootY = level1Dog.StandingRootOffset(groundY);
        dogObject.transform.position = new Vector2(dogX, rootY);
        level1Dog.BuildDog();
        FactionStamp.Wolf(dogObject);
        if (Level1LookFlags.DogBehind)
            dogObject.AddComponent<Level1DogSortShift>();
        ApplyDogGains(level1Dog.stance);
        // req-235 R1 (default off): the hero line's -neckP 2.5 leaks into the dog through the
        // shared flag name (first-wins). The dog keeps its OWN gains when this switch is on.
        if (GetFloatArg("-level1DogOwnGains", 0f) > 0.5f && level1Dog.stance != null)
        {
            level1Dog.stance.neckPGain = 1.5f;
            level1Dog.stance.fallCoMOffset = 0.30f;
        }
        // Walk = the S13 pose line (req-212/216): the clock on, the line's values.
        DogStanceController dc = level1Dog.stance;
        if (dc != null)
        {
            dc.dogPoseOn = true;
            dc.poseAmp = GetFloatArg("-dogPoseAmp", 24f);
            dc.poseT = GetFloatArg("-dogPoseT", 0.55f);
            dc.poseDuty = GetFloatArg("-dogPoseDuty", 0.65f);
            dc.poseFold = GetFloatArg("-dogPoseFold", 36f);
            dc.posePelvisHoldP = GetFloatArg("-dogPosePelvisP", 3f);
        }
        level1DogDrive = dogObject.GetComponent<DogDrive>();
        int seed = Mathf.RoundToInt(GetFloatArg("-level1DogSeed", 1f));
        if (level1DogDrive != null)
        {
            level1DogDrive.allowHeadless = true;
            level1DogDrive.ConfigureLevel1Brain(seed,
                GetFloatArg("-level1DogChaseP", 0.35f),
                GetFloatArg("-level1DogChaseMax", 4f),
                GetFloatArg("-level1DogSniffP", 0.15f),
                GetFloatArg("-level1DogNoKill", 1f) > 0.5f);
            // ROUND 2 switch: chase only a mark the dog can reach + the flight-mode give-up.
            level1DogDrive.level1ChaseGround = GetFloatArg("-level1DogChaseGround", 0f) > 0.5f;
        }
        // Hero <-> dog: every collider pair ignored (the flock's own recipe).
        Collider2D[] heroCols = human != null
            ? human.GetComponentsInChildren<Collider2D>() : new Collider2D[0];
        Collider2D[] dogCols = dogObject.GetComponentsInChildren<Collider2D>();
        for (int a = 0; a < heroCols.Length; a++)
            for (int b = 0; b < dogCols.Length; b++)
                Physics2D.IgnoreCollision(heroCols[a], dogCols[b], true);
        Debug.Log("LEVEL1 dog built x=" + dogX.ToString("F2", CultureInfo.InvariantCulture)
                  + " y=" + rootY.ToString("F2", CultureInfo.InvariantCulture)
                  + " seed=" + seed.ToString(CultureInfo.InvariantCulture)
                  + " chaseP=" + GetFloatArg("-level1DogChaseP", 0.35f).ToString("0.##", CultureInfo.InvariantCulture)
                  + " sniffP=" + GetFloatArg("-level1DogSniffP", 0.15f).ToString("0.##", CultureInfo.InvariantCulture)
                  + " chaseMax=" + GetFloatArg("-level1DogChaseMax", 4f).ToString("0.##", CultureInfo.InvariantCulture)
                  + " noKill=" + (GetFloatArg("-level1DogNoKill", 1f) > 0.5f ? 1 : 0)
                  + " chaseGround=" + (GetFloatArg("-level1DogChaseGround", 0f) > 0.5f ? 1 : 0));
    }

    // req-217 step 2 instrumentation: the flock's answer to the hero's approach, read
    // on the same step the recorder samples. Picture-free - the rows land beside the
    // take as <label>_level1.csv. Nothing here runs without -level1.
    private void SampleLevel1Crows(float t)
    {
        if (level1Crows == null || human == null)
            return;
        Vector2 heroPos = level1HeroBody != null ? level1HeroBody.position : (Vector2)human.transform.position;
        if (level1CrossT < 0f && heroPos.x >= Level1CrossX)
            level1CrossT = t;
        level1Rows.Append(t.ToString("F3", CultureInfo.InvariantCulture));
        level1Rows.Append(',').Append(heroPos.x.ToString("F3", CultureInfo.InvariantCulture));
        for (int i = 0; i < level1Crows.Length; i++)
        {
            Bird crow = level1Crows[i];
            Rigidbody2D body = level1CrowBodies != null ? level1CrowBodies[i] : null;
            if (crow == null || body == null)
            {
                // A crow that failed its build keeps its columns, far away (9.99).
                level1Rows.Append(",9.99,0");
                continue;
            }
            Vector2 p = body.position;
            float d = Vector2.Distance(p, heroPos);
            if (level1MinDist < 0f || d < level1MinDist)
                level1MinDist = d;
            // Airborne = the bird's own flight mode (Fly / Glide): a perched or walking
            // crow is on the tree or the dirt, not in the air.
            bool air = crow.controller != null
                && (crow.controller.mode == BirdMode.Fly || crow.controller.mode == BirdMode.Glide);
            level1Rows.Append(',').Append(d.ToString("F2", CultureInfo.InvariantCulture));
            level1Rows.Append(',').Append(air ? '1' : '0');
        }
        // LEVEL1 DOG (req-231): the dog's own columns, same step as the flock's.
        if (level1Dog != null)
        {
            Vector2 dp = level1Dog.GetFollowPoint();
            float dd = Vector2.Distance(dp, heroPos);
            if (level1DogMinDist < 0f || dd < level1DogMinDist)
                level1DogMinDist = dd;
            DogStanceController dc = level1Dog.stance;
            int st = dc != null ? (int)dc.currentState : -1;
            if (level1DogFellT < 0f && (st == 2 || st == 3))
                level1DogFellT = t;
            level1Rows.Append(',').Append(dp.x.ToString("F3", CultureInfo.InvariantCulture));
            level1Rows.Append(',').Append(dd.ToString("F3", CultureInfo.InvariantCulture));
            level1Rows.Append(',').Append(dp.y.ToString("F3", CultureInfo.InvariantCulture));
            level1Rows.Append(',').Append(st);
        }
        level1Rows.Append('\n');
        SampleLevel1Diag(t, heroPos);
    }

    // req-237 Part 1 (read-only, no behaviour): the flock's own answer to "where is the
    // hero" next to the hero BODY's x, and per crow the support state read from the
    // sensors + speed from the body. A crow that turns airborne gets one event row with
    // the cause names armed at that instant (startle / play / keepAway / mob / alert /
    // wander), the same windows the flock's overlay acts on.
    private void SampleLevel1Diag(float t, Vector2 heroPos)
    {
        BirdFlockDrive f = level1FlockDrive;
        if (f == null || level1Crows == null)
            return;
        if (level1DiagRows == null)
            level1DiagRows = new StringBuilder();
        if (level1AirPrev == null || level1AirPrev.Length != level1Crows.Length)
            level1AirPrev = new int[level1Crows.Length];
        float seeX = f.Level1HasPlayer ? f.Level1PlayerSeeX : float.NaN;
        level1DiagRows.Append(t.ToString("F3", CultureInfo.InvariantCulture));
        level1DiagRows.Append(',').Append(heroPos.x.ToString("F3", CultureInfo.InvariantCulture));
        level1DiagRows.Append(',').Append(float.IsNaN(seeX)
            ? "nan" : seeX.ToString("F3", CultureInfo.InvariantCulture));
        for (int i = 0; i < level1Crows.Length; i++)
        {
            Rigidbody2D body = level1CrowBodies != null ? level1CrowBodies[i] : null;
            if (level1Crows[i] == null || body == null)
            {
                level1DiagRows.Append(",none,0,0,0,0,0,0,0,0,0");
                continue;
            }
            bool perch, ground, keep, hasP, ts, bg;
            int wt;
            float alert;
            f.Level1MemberDiag(i, out perch, out ground, out keep, out wt, out alert,
                out hasP, out ts, out bg);
            Vector2 p = body.position;
            float v = body.linearVelocity.magnitude;
            string st = perch ? "P" : (ground ? "G" : "A");
            float diagHome, diagLeash, diagFace;
            int diagPhase;
            f.Level1MemberLeashDiag(i, out diagHome, out diagLeash, out diagFace, out diagPhase);
            level1DiagRows.Append(',').Append(st)
                .Append(',').Append(p.x.ToString("F2", CultureInfo.InvariantCulture))
                .Append(',').Append(p.y.ToString("F2", CultureInfo.InvariantCulture))
                .Append(',').Append(v.ToString("F2", CultureInfo.InvariantCulture))
                .Append(',').Append(keep ? '1' : '0')
                .Append(',').Append(wt.ToString(CultureInfo.InvariantCulture))
                .Append(',').Append(diagPhase.ToString(CultureInfo.InvariantCulture))
                .Append(',').Append(diagFace.ToString("0", CultureInfo.InvariantCulture))
                .Append(',').Append(diagHome.ToString("F2", CultureInfo.InvariantCulture))
                .Append(',').Append(diagLeash.ToString("F2", CultureInfo.InvariantCulture));
            if (!(level1AirPrev[i] != 0) && st == "A")
            {
                if (level1TakeoffRows == null)
                    level1TakeoffRows = new StringBuilder();
                level1TakeoffRows.Append(t.ToString("F3", CultureInfo.InvariantCulture))
                    .Append(',').Append((i + 1).ToString(CultureInfo.InvariantCulture))
                    .Append(',').Append(Level1Cause(i, t, keep, alert, p))
                    .Append(',').Append(p.x.ToString("F2", CultureInfo.InvariantCulture))
                    .Append(',').Append(p.y.ToString("F2", CultureInfo.InvariantCulture))
                    .Append(',').Append(v.ToString("F2", CultureInfo.InvariantCulture))
                    .Append('\n');
            }
            level1AirPrev[i] = st == "A" ? 1 : 0;
        }
        level1DiagRows.Append('\n');
        if (t >= level1NextSeeLog)
        {
            level1NextSeeLog = Mathf.Floor(t) + 1f;
            Debug.Log("LEVEL1 PLAYERX t=" + t.ToString("F1", CultureInfo.InvariantCulture)
                + " see=" + (float.IsNaN(seeX)
                    ? "nan" : seeX.ToString("F2", CultureInfo.InvariantCulture))
                + " pelvis=" + heroPos.x.ToString("F2", CultureInfo.InvariantCulture)
                + " startle=" + f.Level1StartleUntil.ToString("F1", CultureInfo.InvariantCulture)
                + " mob=" + f.Level1MobUntil.ToString("F1", CultureInfo.InvariantCulture));
        }
    }

    private string Level1Cause(int i, float t, bool keep, float alert, Vector2 p)
    {
        BirdFlockDrive f = level1FlockDrive;
        StringBuilder sb = new StringBuilder(24);
        if (t >= f.Level1StartleBegin && t < f.Level1StartleUntil)
            sb.Append("startle");
        if (t < f.Level1PlayUntil && (i == f.Level1PlayA || i == f.Level1PlayB))
            Level1CauseAdd(sb, "play");
        if (keep)
            Level1CauseAdd(sb, "keepAway");
        if (t < f.Level1MobUntil)
            Level1CauseAdd(sb, "mob");
        if (t < alert)
            Level1CauseAdd(sb, "alert");
        if (f.drumStartleRange > 0.05f)
        {
            float drumX;
            if (BirdSocial.Drumming(t, out drumX) && Mathf.Abs(p.x - drumX) < f.drumStartleRange)
                Level1CauseAdd(sb, "drum");
        }
        if (sb.Length == 0)
            sb.Append("wander");
        return sb.ToString();
    }

    private static void Level1CauseAdd(StringBuilder sb, string cause)
    {
        if (sb.Length > 0)
            sb.Append('+');
        sb.Append(cause);
    }

    // req-237 Part 2: the resettle sites. The fence rail is picture only (no
    // collider) - the crows use its west stretch (slots every 0.35 m, top
    // -1.515), the lamp top and the dirt, all out of the hero's release radius
    // (keepAwayRange * keepAwayRelease = 6.09 m) once he stands under the crown
    // (x ~ 1.08). Entries repeat to weight the seeded draw: fence 10, dirt 4,
    // lamp 1.
    private static Vector2[] Level1SiteSlots()
    {
        return new Vector2[]
        {
            new Vector2(-9.00f, -1.515f), new Vector2(-8.65f, -1.515f),
            new Vector2(-8.30f, -1.515f), new Vector2(-7.95f, -1.515f),
            new Vector2(-7.60f, -1.515f), new Vector2(-7.25f, -1.515f),
            new Vector2(-6.90f, -1.515f), new Vector2(-6.55f, -1.515f),
            new Vector2(-6.20f, -1.515f), new Vector2(-5.85f, -1.515f),
            new Vector2(-8.50f, 0f), new Vector2(-7.50f, 0f),
            new Vector2(-6.50f, 0f), new Vector2(-5.80f, 0f),
            new Vector2(3.25f, 1.08f)
        };
    }

    private static int[] Level1SiteKinds()
    {
        return new int[]
        {
            1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 3, 3, 3, 3, 2
        };
    }

    // req-237 r2/r3 (-level1CrowLife only): the resettle sites need a REAL pad the
    // foot probe and the perch catch can hit - the fence rail is picture only, so
    // round 1's fence crows hovered at the site x and never read support (K3 0/12).
    // Trigger pads on the fence slot band and the lamp top, PlantTree's own
    // EnsureBirdPerches pattern (0.12 x 0.024, Ground layer, trigger). The rail
    // picture itself stays collider-free. r3: the pad sits 0.11 m under the rail
    // top - at the exact top the hero's swinging hands touched the band at the
    // spawn (handContact 0 -> 1 at t 0.04, K5).
    private void Level1BuildSitePads()
    {
        Vector2[] sites = Level1SiteSlots();
        int[] kinds = Level1SiteKinds();
        int made = 0;
        for (int i = 0; i < sites.Length; i++)
        {
            if (kinds[i] != 1 && kinds[i] != 2)
                continue;
            GameObject pad = new GameObject("BirdPerch");
            pad.transform.SetParent(demoLevelRoot, true);
            pad.transform.position = new Vector3(sites[i].x, sites[i].y - 0.11f, 0f);
            pad.AddComponent<BirdPerch>();
            BoxCollider2D col = pad.AddComponent<BoxCollider2D>();
            col.size = new Vector2(0.12f, 0.024f);
            col.isTrigger = true;
            GroundLayers.Apply(pad);
            made++;
        }
        Debug.Log("LEVEL1 site pads=" + made);
    }

    private int CountLevel1Crows()
    {
        if (level1Crows == null)
            return 0;
        int n = 0;
        for (int i = 0; i < level1Crows.Length; i++)
            if (level1Crows[i] != null)
                n++;
        return n;
    }

    // req-246 (LEVEL 1 N3): the storyteller camera. The state runs on the fixed
    // clock beside the crow sampler (reads only); the rows land per SHOT, written
    // by the frame capture itself so row N and shot PNG N share one index.
    private void TickLevel1Cam(float t)
    {
        Vector2 hero = Level1HeroPoint();
        Vector2 crown = Vector2.zero;
        bool crownOn = false;
        if (Level1CrownTop(out crown) && level1AppleTree != null)
            crownOn = Mathf.Abs(hero.x - level1AppleTree.transform.position.x) <= 8f;

        Vector2 flock = Vector2.zero;
        bool flockOn = false;
        int air = 0;
        int built = 0;
        if (level1Crows != null)
        {
            for (int i = 0; i < level1Crows.Length; i++)
            {
                Rigidbody2D body = level1CrowBodies != null ? level1CrowBodies[i] : null;
                if (level1Crows[i] == null || body == null)
                    continue;
                flock += body.position;
                built++;
                if (Level1CrowAir(level1Crows[i]))
                    air++;
            }
            if (built > 0)
            {
                flock /= built;
                flockOn = air * 2 >= built;   // >= 50 % of the crows in the AIR
            }
        }

        Vector2 chest = Vector2.zero;
        bool dogOn = false;
        if (level1Dog != null)
        {
            chest = Level1DogChestPoint();
            dogOn = Vector2.Distance(chest, hero) > 4f;
        }
        level1StoryCam.Tick(Time.fixedDeltaTime, hero, crownOn, crown, flockOn, flock, dogOn, chest);
    }

    private Vector2 Level1HeroPoint()
    {
        if (human == null)
            return Vector2.zero;
        if (human.comCalculator != null)
            return human.comCalculator.GetCenterOfMass();
        return human.transform.position;
    }

    private static bool Level1CrowAir(Bird crow)
    {
        return crow.controller != null
            && (crow.controller.mode == BirdMode.Fly || crow.controller.mode == BirdMode.Glide);
    }

    private Vector2 Level1DogChestPoint()
    {
        HumanSegment seg = level1Dog.chestSegment;
        if (seg != null && seg.rb != null)
            return seg.rb.worldCenterOfMass;
        return level1Dog.GetFollowPoint();
    }

    private bool Level1CrownTop(out Vector2 top)
    {
        top = Vector2.zero;
        return level1AppleTree != null && level1AppleTree.TryCrownTop(out top);
    }

    // The shot hook: paint the state onto the render camera, then log the row.
    // heroIn reads the 10 % safe rect; dogIn / crownIn / crowsIn the frame edge.
    // req-265: the shot index and its dedupe, split out so the steady path can arm a
    // frame and write its row later (after LateUpdate) with the pose that is drawn.
    private bool ArmLevel1CamShot(string path)
    {
        string name = Path.GetFileNameWithoutExtension(path);
        int us = name.LastIndexOf('_');
        int idx = -1;
        if (us >= 0)
            int.TryParse(name.Substring(us + 1), NumberStyles.Integer, CultureInfo.InvariantCulture, out idx);
        if (idx < 0 || idx == level1CamLastShot)
            return false;
        level1CamLastShot = idx;
        return true;
    }

    // What this pose holds: the hero in its safe rect, the dog chest, the crown top,
    // the share of the built flock inside the lens.
    private void Level1CamFrameCounters(float camX, float camY, float ortho,
                                        out int heroIn, out int dogIn, out int crownIn, out float crowsIn)
    {
        Level1StoryCam c = level1StoryCam;
        float hw = ortho * c.Aspect;
        float hh = ortho;
        Vector2 hero = Level1HeroPoint();
        heroIn = Mathf.Abs(hero.x - camX) <= hw * (1f - c.Margin)
                 && Mathf.Abs(hero.y - camY) <= hh * (1f - c.Margin) ? 1 : 0;
        dogIn = 0;
        if (level1Dog != null)
        {
            Vector2 chest = Level1DogChestPoint();
            dogIn = Mathf.Abs(chest.x - camX) <= hw && Mathf.Abs(chest.y - camY) <= hh ? 1 : 0;
        }
        crownIn = 0;
        Vector2 crown;
        if (Level1CrownTop(out crown))
            crownIn = Mathf.Abs(crown.x - camX) <= hw && Mathf.Abs(crown.y - camY) <= hh ? 1 : 0;
        int inFrame = 0;
        int builtN = 0;
        if (level1Crows != null)
        {
            for (int i = 0; i < level1Crows.Length; i++)
            {
                Rigidbody2D body = level1CrowBodies != null ? level1CrowBodies[i] : null;
                if (level1Crows[i] == null || body == null)
                    continue;
                builtN++;
                Vector2 p = body.position;
                if (Mathf.Abs(p.x - camX) <= hw && Mathf.Abs(p.y - camY) <= hh)
                    inFrame++;
            }
        }
        crowsIn = builtN > 0 ? (float)inFrame / builtN : 0f;
    }

    private void AppendLevel1CamRow(float camX, float camY, float ortho,
                                    int heroIn, int dogIn, int crownIn, float crowsIn)
    {
        if (level1CamRows == null)
            level1CamRows = new StringBuilder(128);
        level1CamRows.Append((humanSteps * Time.fixedDeltaTime).ToString("F3", CultureInfo.InvariantCulture)).Append(',')
            .Append(camX.ToString("F3", CultureInfo.InvariantCulture)).Append(',')
            .Append(camY.ToString("F3", CultureInfo.InvariantCulture)).Append(',')
            .Append(ortho.ToString("F3", CultureInfo.InvariantCulture)).Append(',')
            .Append(heroIn).Append(',').Append(dogIn).Append(',').Append(crownIn).Append(',')
            .Append(crowsIn.ToString("F3", CultureInfo.InvariantCulture)).Append('\n');
    }

    private void ApplyLevel1CamShot(Camera cam, string path)
    {
        if (!ArmLevel1CamShot(path))
            return;

        cam.orthographic = true;
        cam.transform.position = new Vector3(level1StoryCam.CamX, level1StoryCam.CamY, -10f);
        cam.orthographicSize = level1StoryCam.Ortho;

        Level1StoryCam c = level1StoryCam;
        int heroIn, dogIn, crownIn;
        float crowsIn;
        Level1CamFrameCounters(c.CamX, c.CamY, c.Ortho, out heroIn, out dogIn, out crownIn, out crowsIn);
        AppendLevel1CamRow(c.CamX, c.CamY, c.Ortho, heroIn, dogIn, crownIn, crowsIn);
    }

    // req-265 steady path: the shot only ARMS the frame. The row is written after the
    // frame's LateUpdate, so the PNG, the row and the window share one pose.
    private void ArmSteadyLevel1CamShot(Camera cam, string path)
    {
        level1CamSteadyShot = ArmLevel1CamShot(path);
        if (!level1CamSteadyShot)
            return;
        if (level1CamDraw == null)
            level1CamDraw = cam.gameObject.AddComponent<Level1CamSteady>();
        level1CamDraw.Bind(level1StoryCam);
    }

    private void FlushSteadyLevel1CamRow(Camera cam)
    {
        if (!level1CamSteadyShot)
            return;
        level1CamSteadyShot = false;
        Vector3 p = cam.transform.position;
        float ortho = cam.orthographicSize;
        int heroIn, dogIn, crownIn;
        float crowsIn;
        Level1CamFrameCounters(p.x, p.y, ortho, out heroIn, out dogIn, out crownIn, out crowsIn);
        AppendLevel1CamRow(p.x, p.y, ortho, heroIn, dogIn, crownIn, crowsIn);
    }

    private void WriteLevel1Section(string label)
    {
        StringBuilder head = new StringBuilder(160);
        head.Append("t,heroX");
        for (int i = 0; i < level1Crows.Length; i++)
            head.Append(",d").Append(i + 1).Append(",air").Append(i + 1);
        // The dog's own columns ride only a -level1Dog take, so a crow take keeps every byte.
        if (level1Dog != null)
            head.Append(",dogX,dogDist,dogY,dogState");
        head.Append('\n').Append(level1Rows);
        string csvPath = Path.Combine(outputFolder, label + "_level1.csv");
        File.WriteAllText(csvPath, head.ToString());

        StringBuilder json = new StringBuilder(160);
        json.Append("{\"label\":\"").Append(label).Append('"');
        json.Append(",\"crows\":").Append(level1Crows.Length);
        json.Append(",\"crossT\":").Append(level1CrossT.ToString("0.###", CultureInfo.InvariantCulture));
        json.Append(",\"crossX\":").Append(Level1CrossX.ToString("0.##", CultureInfo.InvariantCulture));
        json.Append(",\"nearM\":").Append(Level1NearMeters.ToString("0.##", CultureInfo.InvariantCulture));
        json.Append(",\"minDist\":").Append(level1MinDist.ToString("0.###", CultureInfo.InvariantCulture));
        // The flock's own counters: the "own life" evidence (walk / peck / land) next
        // to the takeoffs the judge reads from the rows.
        if (level1FlockDrive != null)
        {
            json.Append(",\"takeoffCount\":").Append(level1FlockDrive.TakeoffCount);
            json.Append(",\"walkCount\":").Append(level1FlockDrive.WalkCount);
            json.Append(",\"peckCount\":").Append(level1FlockDrive.PeckCount);
            json.Append(",\"landCount\":").Append(level1FlockDrive.LandCount);
            json.Append(",\"perchLandCount\":").Append(level1FlockDrive.PerchLandCount);
            // req-316: the yard band's own ledger, written only with the flag on.
            if (level1YardBound)
            {
                json.Append(",\"level1YardTurns\":").Append(level1FlockDrive.Level1YardTurns);
                json.Append(",\"level1YardFlights\":").Append(level1FlockDrive.Level1YardFlights);
                json.Append(",\"level1CrowsOffTape\":").Append(level1FlockDrive.Level1CrowsOffTape);
                json.Append(",\"level1CrowsLost\":").Append(level1FlockDrive.Level1CrowsLost);
            }

            // req-319: the fear counters exist only with the flag, so every other
            // level take's JSON keeps every byte.
            if (level1FlockDrive.levelCrowFear)
            {
                json.Append(",\"crowFearFlushes\":").Append(level1FlockDrive.CrowFearFlushes);
                json.Append(",\"crowFearReturns\":").Append(level1FlockDrive.CrowFearReturns);
                json.Append(",\"crowFearAlerts\":").Append(level1FlockDrive.CrowFearAlerts);
                json.Append(",\"crowFearMinGroundDxHero\":").Append(
                    level1FlockDrive.CrowFearMinGroundDxHero.ToString("0.###", CultureInfo.InvariantCulture));
                json.Append(",\"crowFearMinGroundDDog\":").Append(
                    level1FlockDrive.CrowFearMinGroundDDog.ToString("0.###", CultureInfo.InvariantCulture));
            }
            // req-331 step 3 (kept by req-365 4a): the own-hop counter only.
            if (level1Perch)
            {
                json.Append(",\"perchHop\":").Append(level1FlockDrive.PerchHops);
            }
        }
        if (level1Dog != null)
        {
            json.Append(",\"dogX\":").Append(level1Dog.GetFollowPoint().x.ToString("0.###", CultureInfo.InvariantCulture));
            json.Append(",\"dogMinDist\":").Append(level1DogMinDist.ToString("0.###", CultureInfo.InvariantCulture));
            json.Append(",\"dogFellT\":").Append(level1DogFellT.ToString("0.###", CultureInfo.InvariantCulture));
            if (level1DogDrive != null)
            {
                json.Append(",\"dogChases\":").Append(level1DogDrive.Level1Chases);
                json.Append(",\"dogGiveUps\":").Append(level1DogDrive.Level1GiveUps);
                json.Append(",\"dogSniffs\":").Append(level1DogDrive.Level1Sniffs);
            }
        }
        json.Append('}');
        File.WriteAllText(Path.Combine(outputFolder, label + "_level1.json"), json.ToString());
        // req-237 Part 1 diag files (new, separate): the flock's own hero read + the
        // per-crow support state, and one row per takeoff with its cause names.
        if (level1DiagRows != null)
        {
            StringBuilder dhead = new StringBuilder(160);
            dhead.Append("t,heroX,seeX");
            for (int i = 0; i < level1Crows.Length; i++)
                dhead.Append(",s").Append(i + 1).Append(",x").Append(i + 1).Append(",y").Append(i + 1)
                    .Append(",v").Append(i + 1).Append(",kw").Append(i + 1).Append(",wtk").Append(i + 1)
                    .Append(",ph").Append(i + 1).Append(",fc").Append(i + 1)
                    .Append(",hm").Append(i + 1).Append(",lh").Append(i + 1);
            dhead.Append('\n').Append(level1DiagRows);
            string diagPath = Path.Combine(outputFolder, label + "_level1diag.csv");
            File.WriteAllText(diagPath, dhead.ToString());
            string tkPath = Path.Combine(outputFolder, label + "_level1tk.csv");
            File.WriteAllText(tkPath, new StringBuilder(64).Append("t,id,cause,x,y,v\n")
                .Append(level1TakeoffRows != null ? level1TakeoffRows.ToString() : "").ToString());
            Debug.Log("TRIAL_LEVEL1DIAG_CSV " + diagPath);
            Debug.Log("TRIAL_LEVEL1TK_CSV " + tkPath);
        }
        // req-246: the storyteller camera's own CSV - one row per shot (t, centre,
        // lens, and what was inside the frame at that shot).
        if (level1CamRows != null)
        {
            string camPath = Path.Combine(outputFolder, label + "_level1cam.csv");
            File.WriteAllText(camPath, new StringBuilder(160)
                .Append("t,camX,camY,ortho,heroIn,dogIn,crownIn,crowsIn\n")
                .Append(level1CamRows.ToString()).ToString());
            Debug.Log("TRIAL_LEVEL1CAM_CSV " + camPath);
        }
        // req-265: the per-frame drawn pose (only under -level1CamSteady) - the plot's
        // "after" curve and the C8 frame-to-frame check read this file.
        if (level1CamDraw != null)
        {
            string drawPath = Path.Combine(outputFolder, label + "_level1camdraw.csv");
            File.WriteAllText(drawPath, new StringBuilder(64)
                .Append("t,camX,camY,ortho\n").Append(level1CamDraw.DrawLog()).ToString());
            Debug.Log("TRIAL_LEVEL1CAM_DRAW_CSV " + drawPath);
        }
        Debug.Log("TRIAL_LEVEL1_CSV " + csvPath);
    }

    // ARENA-1: the arena lives on its own host. The ground is already built by
    // BuildTrialScene, so the runner must not build a second one.
    private void BuildArenaScene()
    {
        GameObject host = new GameObject("ArenaRunner");
        arena = host.AddComponent<ArenaRunner>();
        arena.seed = arenaSeed;
        arena.label = label;
        arena.runId = label + "_s" + arenaSeed.ToString(CultureInfo.InvariantCulture);
        arena.sourceFingerprint = runInfo != null ? runInfo.SourceFingerprint : "";
        arena.buildGuid = runInfo != null ? runInfo.ManifestBuildGuid : "";
        arena.spawnInterval = arenaInterval;
        // WALK CYCLE 58: 15 -> 20 s. The fitness window starts at the walk onset
        // (WalkOnsetSeconds = 5 s; Yury's onset rule) and the post-onset window is a
        // full 15 s, so the measure mark sits at spawn + 20 s.
        arena.measureSeconds = 20f;
        arena.lawMode = arenaLaw;
        arena.liftFloorMeters = arenaLiftFloor;
        arena.clearShareBar = arenaClearShare;
        arena.cullSeconds = 30f;
        arena.aliveCap = 60;
        arena.durationSeconds = arenaSeconds;
        arena.spawnX = 0f;
        arena.spawnY = startY;
        arena.groundWidth = arenaGroundMeters;
        arena.numberHeight = arenaNumberHeight;
        arena.cameraSize = arenaCameraSize;
        arena.cameraOffsetX = arenaCameraOffsetX;
        arena.photoCameraSize = arenaPhotoSize;
        arena.photoOffsetX = arenaPhotoOffsetX;
        // ARENA CYCLE 59: the lens rides with the furthest alive body (Yury's rule).
        arena.cameraFollowLeader = arenaCamFollow;
        arena.cameraLeadMeters = arenaLead;
        arena.externalTick = true;
        arena.headless = true;
        arena.outputFolder = outputFolder;
        arena.buildGroundOnBegin = false;
    }

    // DOG ARENA (cycle 64): the arena lives on its own host. The ground is already built by
    // BuildTrialScene. Begin() here, in the scene build, is deliberate: the reference body (id 1)
    // must exist BEFORE the first physics step, exactly as the trial's own dog does — that is what
    // makes its trace comparable with the record row's CSV step for step.
    private void BuildDogArenaScene()
    {
        GameObject host = new GameObject("DogArenaRunner");
        dogArena = host.AddComponent<DogArenaRunner>();
        dogArena.seed = dogArenaSeed;
        dogArena.label = label;
        dogArena.runId = label + "_s" + dogArenaSeed.ToString(CultureInfo.InvariantCulture);
        dogArena.sourceFingerprint = runInfo != null ? runInfo.SourceFingerprint : "";
        dogArena.buildGuid = runInfo != null ? runInfo.ManifestBuildGuid : "";
        dogArena.spawnInterval = dogArenaInterval;
        dogArena.measureSeconds = dogArenaMeasure;
        dogArena.cullSeconds = dogArenaCull;
        dogArena.aliveCap = 60;
        dogArena.durationSeconds = dogArenaSeconds;
        dogArena.spawnX = 0f;
        dogArena.groundTopY = -2f;
        dogArena.cameraSize = arenaCameraSize;
        dogArena.cameraOffsetX = arenaCameraOffsetX;
        dogArena.outputFolder = outputFolder;
        dogArena.injectGenome = dogArenaInject;
        dogArena.isolationMode = dogArenaIsolation;
        dogArena.Begin(Time.fixedDeltaTime);
    }

    // DOG ARENA (cycle 64): the video/deadline loop, mirrored from RunDogTrial — the arena's own
    // clock and sampling live in FixedUpdate (see there); this loop only owns the frames and the
    // real-time guard, exactly as the dog trial's own coroutine does.
    private IEnumerator RunDogArenaTrial()
    {
        if (dogArena == null)
        {
            Debug.LogError("HeadlessTrial: -dogArena 1 without a DogArenaRunner");
            yield break;
        }
        if (!dogArena.Ready)
            dogArena.Begin(Time.fixedDeltaTime);
        if (visual)
            dogArena.EnsureArenaCamera(dogArena.cameraSize, dogArena.cameraOffsetX);

        float wallStart = Time.realtimeSinceStartup;
        int lastShot = -1;
        dogArenaSteps = 0;
        dogArenaStepping = true;
        while (dogArenaStepping)
        {
            yield return FixedStep;
            float simulated = dogArenaSteps * Time.fixedDeltaTime;

            if (visual)
            {
                int shotIdx = (int)(simulated / shotPeriod);
                if (shotIdx != lastShot)
                {
                    lastShot = shotIdx;
                    string shotPath = Path.Combine(outputFolder,
                        "shot_" + label + "_" + shotIdx.ToString("00") + ".png");
                    yield return CaptureVisualFrame(shotPath);
                }
            }

            if (Time.realtimeSinceStartup - wallStart > dogArenaWallLimit)
            {
                Debug.LogWarning("HeadlessTrial: dog arena wall limit " + dogArenaWallLimit + " s hit at sim "
                    + simulated.ToString("0.##", CultureInfo.InvariantCulture) + " s, run cut short.");
                dogArenaStepping = false;
                break;
            }
        }

        Finish(dogArenaSteps * Time.fixedDeltaTime, Time.realtimeSinceStartup - wallStart);
    }

    private void BuildDogScene()
    {
        GameObject dogObject = new GameObject("TrialDog");
        dog = dogObject.AddComponent<Dog>();
        ApplyDogBuildOverrides(dog);
        // Height follows the build pose, the way GameProcess does it. A
        // spawn angle changes the column drop, and the pre-build default
        // would then bury the paws or drop the dog in from the air.
        if (!HasArg("-startY"))
        {
            startY = dog.StandingRootOffset(-2f);
            // runInfo is filled by the provenance check, before any body
            // exists. Without this the summary reports a spawn height the
            // run never used, which is the defect this pass came to fix.
            if (runInfo != null)
                runInfo.StartY = startY;
        }
        dogObject.transform.position = new Vector2(0f, startY);
        dog.BuildDog();
        FactionStamp.Wolf(dogObject);
        ApplyDogGains(dog.stance);

        if (dogLeap || HasArg("-dogPreyX"))
        {
            DogPrey prey = DogPrey.Spawn(new Vector2(dogPreyX, -2f + DogPrey.DefaultHeightAboveGround));
            if (dog.stance != null && prey != null)
                dog.stance.leapTarget = prey.transform;
        }

        Transform chest = dog.transform.Find("Chest");
        torsoBody = chest != null ? chest.GetComponent<Rigidbody2D>() : null;
    }

    private static void ApplyDogBuildOverrides(Dog target)
    {
        if (target == null) return;
        float muscle = GetFloatArg("-muscle", 1f);
        float friction = GetFloatArg("-friction", 1f);
        target.lumbarMuscleTorque *= muscle;
        target.neckMuscleTorque *= muscle;
        target.tailMuscleTorque *= muscle;
        target.hipMuscleTorque *= muscle;
        target.kneeMuscleTorque *= muscle;
        target.shoulderMuscleTorque *= muscle;
        target.elbowMuscleTorque *= muscle;
        target.pawExtensorTorque *= muscle;
        target.pawFlexorTorque *= muscle;
        target.lumbarFriction *= friction;
        target.neckFriction *= friction;
        target.tailFriction *= friction;
        target.hipFriction *= friction;
        target.kneeFriction *= friction;
        target.shoulderFriction *= friction;
        target.elbowFriction *= friction;
        target.pawFriction *= friction;
        target.neckFriction = GetFloatArg("-neckFriction", target.neckFriction);
        target.hipFriction = GetFloatArg("-hipFriction", target.hipFriction);
        target.kneeFriction = GetFloatArg("-kneeFriction", target.kneeFriction);
        target.shoulderFriction = GetFloatArg("-shoulderFriction", target.shoulderFriction);
        target.elbowFriction = GetFloatArg("-elbowFriction", target.elbowFriction);
        target.pawFriction = GetFloatArg("-pawFriction", target.pawFriction);
        // Contact materials (collider-materials rule, 18.09). Named apart
        // from -pawFriction above, which is JOINT friction K inside the
        // hinge, not grip: these two set the sharedMaterials the pads and
        // the body plating carry against the ground.
        target.pawContactFriction = GetFloatArg("-dogPawFriction", target.pawContactFriction);
        target.bodyContactFriction = GetFloatArg("-dogBodyFriction", target.bodyContactFriction);
        target.lumbarFriction = GetFloatArg("-lumbarFriction", target.lumbarFriction);
        target.lumbarMuscleTorque = GetFloatArg("-lumbarMuscle", target.lumbarMuscleTorque);
        target.neckMuscleTorque = GetFloatArg("-neckMuscle", target.neckMuscleTorque);
        target.pawExtensorTorque = GetFloatArg("-pawExtensor", target.pawExtensorTorque);
        target.pawFlexorTorque = GetFloatArg("-pawFlexor", target.pawFlexorTorque);
        // Limb rake without a rebuild. Pair each with the matching
        // -hipBase / -shoulderBase, or the pose target fights the build.
        target.spawnHipAngle = GetFloatArg("-spawnHip", target.spawnHipAngle);
        target.spawnShoulderAngle = GetFloatArg("-spawnShoulder", target.spawnShoulderAngle);
        target.spawnKneeAngle = GetFloatArg("-spawnKnee", target.spawnKneeAngle);
        target.spawnElbowAngle = GetFloatArg("-spawnElbow", target.spawnElbowAngle);
    }

    private static void ApplyDogGains(DogStanceController c)
    {
        if (c == null)
        {
            Debug.LogError("HeadlessTrial: DogStanceController not found.");
            return;
        }

        c.pawComP = GetFloatArg("-pawComP", c.pawComP);
        c.pawComD = GetFloatArg("-pawComD", c.pawComD);
        c.pawComDeadzone = GetFloatArg("-pawComDead", c.pawComDeadzone);
        c.pawComVelDeadzone = GetFloatArg("-pawComVelDead", c.pawComVelDeadzone);
        c.pawGroundHoldSeconds = GetFloatArg("-pawGroundHold", c.pawGroundHoldSeconds);
        c.chestPGain = GetFloatArg("-chestP", c.chestPGain);
        c.chestDGain = GetFloatArg("-chestD", c.chestDGain);
        c.chestTargetTilt = GetFloatArg("-chestTilt", c.chestTargetTilt);
        c.pelvisPGain = GetFloatArg("-pelvisP", c.pelvisPGain);
        c.pelvisDGain = GetFloatArg("-pelvisD", c.pelvisDGain);
        c.pelvisTargetTilt = GetFloatArg("-pelvisTilt", c.pelvisTargetTilt);
        c.lumbarPGain = GetFloatArg("-lumbarP", c.lumbarPGain);
        c.lumbarDGain = GetFloatArg("-lumbarD", c.lumbarDGain);
        c.lumbarPelvisP = GetFloatArg("-lumbarPelvisP", c.lumbarPelvisP);
        c.lumbarPelvisD = GetFloatArg("-lumbarPelvisD", c.lumbarPelvisD);
        c.lumbarStopP = GetFloatArg("-lumbarStopP", c.lumbarStopP);
        c.lumbarRestP = GetFloatArg("-lumbarRestP", c.lumbarRestP);
        c.lumbarDelaySeconds = GetFloatArg("-lumbarDelay", c.lumbarDelaySeconds);
        c.lumbarOpenGateDeg = GetFloatArg("-lumbarOpenGate", c.lumbarOpenGateDeg);
        c.useLumbarJointRest = GetFloatArg("-lumbarJointRest", c.useLumbarJointRest ? 1f : 0f) > 0.5f;
        c.lumbarJointRestDeg = GetFloatArg("-lumbarRestDeg", c.lumbarJointRestDeg);
        c.hipPGain = GetFloatArg("-hipP", c.hipPGain);
        c.startupHipP = GetFloatArg("-startupHipP", c.startupHipP);
        c.startupHipSeconds = GetFloatArg("-startupHipSec", c.startupHipSeconds);
        c.hipDGain = GetFloatArg("-hipD", c.hipDGain);
        c.kneePGain = GetFloatArg("-kneeP", c.kneePGain);
        c.kneeDGain = GetFloatArg("-kneeD", c.kneeDGain);
        c.hipBaseAngle = GetFloatArg("-hipBase", c.hipBaseAngle);
        c.shoulderBaseAngle = GetFloatArg("-shoulderBase", c.shoulderBaseAngle);
        c.kneeBaseAngle = GetFloatArg("-kneeBase", c.kneeBaseAngle);
        c.elbowBaseAngle = GetFloatArg("-elbowBase", c.elbowBaseAngle);
        c.neckPGain = GetFloatArg("-neckP", c.neckPGain);
        c.neckDGain = GetFloatArg("-neckD", c.neckDGain);
        c.neckTargetTilt = GetFloatArg("-neckTilt", c.neckTargetTilt);
        c.useStablePd = GetFloatArg("-spd", c.useStablePd ? 1f : 0f) > 0.5f;
        c.muscleActivationSpeed = GetFloatArg("-activationSpeed", c.muscleActivationSpeed);
        c.fallCoMOffset = GetFloatArg("-fallOffset", c.fallCoMOffset);
        c.heightP = GetFloatArg("-heightP", c.heightP);
        c.heightD = GetFloatArg("-heightD", c.heightD);
        c.heightMaxDeg = GetFloatArg("-heightMax", c.heightMaxDeg);
        c.heightHipMaxDeg = GetFloatArg("-heightHipMax", c.heightHipMaxDeg);
        c.heightDeadzone = GetFloatArg("-heightDead", c.heightDeadzone);
        c.heightVelDeadzone = GetFloatArg("-heightVelDead", c.heightVelDeadzone);
        c.poseHoldDeadzoneDeg = GetFloatArg("-poseDead", c.poseHoldDeadzoneDeg);
        c.startupHoldSeconds = GetFloatArg("-startupHold", c.startupHoldSeconds);
        c.jawOpen = Mathf.Clamp01(GetFloatArg("-jawOpen", c.jawOpen));
        if (GetFloatArg("-dogBark", 0f) > 0.5f)
            c.Bark();
        if (GetFloatArg("-dogBite", 0f) > 0.5f)
            c.Bite();
        c.walkPeriod = GetFloatArg("-dogWalkPeriod", c.walkPeriod);
        c.walkDuty = GetFloatArg("-dogWalkDuty", c.walkDuty);
        c.walkRearLag = GetFloatArg("-dogWalkRearLag", c.walkRearLag);
        c.walkHipFlex = GetFloatArg("-dogWalkHipFlex", c.walkHipFlex);
        c.walkShoulderFlex = GetFloatArg("-dogWalkShoulderFlex", c.walkShoulderFlex);
        c.walkHipExtend = GetFloatArg("-dogWalkHipExt", c.walkHipExtend);
        c.walkShoulderExtend = GetFloatArg("-dogWalkShoulderExt", c.walkShoulderExtend);
        c.walkKneeFlex = GetFloatArg("-dogWalkKneeFlex", c.walkKneeFlex);
        c.walkElbowFlex = GetFloatArg("-dogWalkElbowFlex", c.walkElbowFlex);
        c.walkPawPlant = GetFloatArg("-dogWalkPlant", c.walkPawPlant);
        c.walkFrontPlantScale = GetFloatArg("-dogWalkFrontPlant", c.walkFrontPlantScale);
        c.walkStanceSweepDeg = GetFloatArg("-dogWalkSweep", c.walkStanceSweepDeg);
        c.walkChestTilt = GetFloatArg("-dogWalkChest", c.walkChestTilt);
        c.walkLumbarP = GetFloatArg("-dogWalkLumbarP", c.walkLumbarP);
        c.walkHipP = GetFloatArg("-dogWalkHipP", c.walkHipP);
        c.walkKneeP = GetFloatArg("-dogWalkKneeP", c.walkKneeP);
        c.walkActivationSpeed = GetFloatArg("-dogWalkAct", c.walkActivationSpeed);
        // CYCLE 71: THE COIL - the get-up family's own flag, DEFAULT OFF. With it
        // absent every accepted command line reproduces its row bit for bit.
        c.coilWanted = GetFloatArg("-dogCoil", 0f) > 0.5f;
        c.runSpeed = GetFloatArg("-dogRunSpeed", c.runSpeed);
        c.runSpeedRef = GetFloatArg("-dogRunSpeedRef", c.runSpeedRef);
        c.runStride = GetFloatArg("-dogRunStride", c.runStride);
        c.runHipFlex = GetFloatArg("-dogRunHipFlex", c.runHipFlex);
        c.runShoulderFlex = GetFloatArg("-dogRunShoulderFlex", c.runShoulderFlex);
        c.runHipExtend = GetFloatArg("-dogRunHipExt", c.runHipExtend);
        c.runShoulderExtend = GetFloatArg("-dogRunShoulderExt", c.runShoulderExtend);
        c.runKneeFlex = GetFloatArg("-dogRunKneeFlex", c.runKneeFlex);
        c.runElbowFlex = GetFloatArg("-dogRunElbowFlex", c.runElbowFlex);
        c.runChestLean = GetFloatArg("-dogRunChestLean", c.runChestLean);
        c.runPawPlant = GetFloatArg("-dogRunPlant", c.runPawPlant);
        // CYCLE 133: THE POSE CLOCK + THE ASSIST - both default off.
        c.dogPoseOn = GetFloatArg("-dogPose", 0f) > 0.5f;
        c.poseT = GetFloatArg("-dogPoseT", c.poseT);
        c.poseDuty = GetFloatArg("-dogPoseDuty", c.poseDuty);
        c.poseAmp = GetFloatArg("-dogPoseAmp", c.poseAmp);
        c.poseAmpF = GetFloatArg("-dogPoseAmpF", c.poseAmpF);
        c.poseFold = GetFloatArg("-dogPoseFold", c.poseFold);
        c.poseSettleSeconds = GetFloatArg("-dogPoseSettle", c.poseSettleSeconds);
        c.poseStopSafe = GetFloatArg("-dogPoseStopSafe", 0f) > 0.5f;
        c.dogStopDiag = GetFloatArg("-dogStopDiag", 0f) > 0.5f;
        c.poseEntrySeconds = GetFloatArg("-dogPoseEntry", c.poseEntrySeconds);
        c.poseStepMaxM = GetFloatArg("-dogPoseStepMax", c.poseStepMaxM);
        c.dogAssistOn = GetFloatArg("-dogAssist", 0f) > 0.5f;
        c.dogAssistFmaxBw = GetFloatArg("-dogAssistFmax", c.dogAssistFmaxBw);
        c.dogAssistTorqueMax = GetFloatArg("-dogAssistTorque", c.dogAssistTorqueMax);
        c.dogAssistMode = (int)GetFloatArg("-dogAssistMode", c.dogAssistMode);
        c.dogAssistDeadDeg = GetFloatArg("-dogAssistDead", c.dogAssistDeadDeg);
        c.posePelvisHoldP = GetFloatArg("-dogPosePelvisP", c.posePelvisHoldP);
        c.poseChestLean = GetFloatArg("-dogPoseChestLean", c.poseChestLean);
        c.poseTameTilt = GetFloatArg("-dogPoseTame", c.poseTameTilt);
    }

    private void BuildBirdScene()
    {
        string rigDefault = spawnDogBird && !HasArg("-birdRig") ? "flock" : "ragdoll";
        string rigRaw = GetStringArg("-birdRig", rigDefault).Trim().ToLowerInvariant();
        bool flockRig = rigRaw == "flock";
        int count = flockRig && (birdDriveKind == "wander" || birdDriveKind == "prey" || treePerch)
            ? birdDriveCount
            : 1;
        // Hunt is one quarry next to the dog, not a pack on the dog's chest.
        if (spawnDog)
            count = 1;
        Bird[] pack = new Bird[count];
        float spacing = BirdPackSpacing;
        float originX = spawnDog
            ? DogHuntBirdOriginX()
            : -0.5f * (count - 1) * spacing;
        float birdY = spawnDog
            ? Bird.KindStandingRootY(trialBirdKind, -2f)
            : startY;

        for (int i = 0; i < count; i++)
        {
            GameObject birdObject = new GameObject(count == 1 ? "TrialBird" : "TrialBird_" + (i + 1));
            birdObject.transform.position = new Vector2(originX + i * spacing, birdY);
            Bird next = birdObject.AddComponent<Bird>();
            next.rig = flockRig ? BirdRig.Flock : BirdRig.Ragdoll;
            next.kind = trialBirdKind;
            next.ApplyKind(trialBirdKind);
            next.minLimbInertia = GetFloatArg("-minInertia", next.minLimbInertia);
            next.minBodyInertia = GetFloatArg("-bodyInertia", next.minBodyInertia);
            birdObject.transform.position = new Vector2(originX + i * spacing, birdY);
            FactionStamp.Bird(birdObject);
            next.BuildBird();
            ApplyBirdGains(next.controller);
            if (next.flight != null)
            {
                next.flight.hoverMean = GetFloatArg("-hover", next.flight.hoverMean);
                next.flight.upstrokeLift = GetFloatArg("-upstrokeScale", next.flight.upstrokeLift);
                next.flight.glideLift = GetFloatArg("-glideLift", next.flight.glideLift);
            }

            if (next.controller != null)
            {
                next.controller.flapPhaseOffset = i * 0.13f;
                if (birdDriveKind == "wander" || birdDriveKind == "prey")
                {
                    next.controller.takeoffDelay = 0f;
                    next.controller.turnAfter = 0f;
                    next.controller.mode = BirdMode.Sit;
                }
            }

            pack[i] = next;
        }

        trialBirds = pack;
        bird = pack[0];
        Transform body = bird.transform.Find("Body");
        birdBody = body != null ? body.GetComponent<Rigidbody2D>() : null;
        torsoBody = birdBody;

        bool driveEach = GetFloatArg("-birdDriveEach", 0f) > 0.5f;
        BirdFlockDrive spawnedFlock = null;
        trialFlockDrive = null;
        if ((birdDriveKind == "wander" || birdDriveKind == "prey") && count > 1 && !driveEach)
        {
            GameObject host = new GameObject("BirdFlockDrive");
            BirdFlockDrive flock = host.AddComponent<BirdFlockDrive>();
            spawnedFlock = flock;
            trialFlockDrive = flock;
            flock.allowHeadless = true;
            flock.seed = birdDriveSeed;
            // Timing before Bind: wander Reset reads sitMin/sitMax.
            flock.sitMin = GetFloatArg("-sitMin", flock.sitMin);
            flock.sitMax = GetFloatArg("-sitMax", flock.sitMax);
            flock.flyMin = GetFloatArg("-flyMin", flock.flyMin);
            flock.flyMax = GetFloatArg("-flyMax", flock.flyMax);
            flock.glideLead = GetFloatArg("-glideLead", flock.glideLead);
            flock.homeFaceDeadzone = GetFloatArg("-homeFaceDeadzone", flock.homeFaceDeadzone);
            flock.flockLeash = GetFloatArg("-flockLeash", flock.flockLeash);
            flock.walkMin = GetFloatArg("-walkMin", flock.walkMin);
            flock.walkMax = GetFloatArg("-walkMax", flock.walkMax);
            flock.aiStride = Mathf.Max(1, Mathf.RoundToInt(GetFloatArg("-aiStride", flock.aiStride)));
            flock.Bind(pack, birdDriveSeed);
            if (plantTree != null)
                flock.BindPerches(plantTree.GetPerchSlots());
        }
        else if ((birdDriveKind == "wander" || birdDriveKind == "prey") && count > 1 && driveEach)
        {
            // Per bird: its own BirdDrive and seed (costlier than a shared flock Update).
            float sitMin = GetFloatArg("-sitMin", 1.4f);
            float sitMax = GetFloatArg("-sitMax", 3.6f);
            float flyMin = GetFloatArg("-flyMin", 2.2f);
            float flyMax = GetFloatArg("-flyMax", 5.5f);
            float glideLead = GetFloatArg("-glideLead", 1.1f);
            float leash = GetFloatArg("-flockLeash", 7f);
            float homeFaceDeadzone = GetFloatArg("-homeFaceDeadzone", 0.15f);
            float walkMin = GetFloatArg("-walkMin", 1.8f);
            float walkMax = GetFloatArg("-walkMax", 3.8f);
            BirdDrive.Kind driveKind = birdDriveKind == "prey"
                ? BirdDrive.Kind.Prey
                : BirdDrive.Kind.Wander;
            for (int i = 0; i < pack.Length; i++)
            {
                if (pack[i] == null) continue;
                BirdDrive drive = pack[i].gameObject.AddComponent<BirdDrive>();
                drive.allowHeadless = true;
                drive.kind = driveKind;
                drive.wanderSeed = birdDriveSeed + i * 17;
                drive.sitMin = sitMin;
                drive.sitMax = sitMax;
                drive.flyMin = flyMin;
                drive.flyMax = flyMax;
                drive.glideLead = glideLead;
                drive.leash = leash;
                drive.homeFaceDeadzone = homeFaceDeadzone;
                drive.walkMin = walkMin;
                drive.walkMax = walkMax;
            }
        }
        else if (birdDriveKind == "wander" || birdDriveKind == "prey")
        {
            BirdDrive drive = bird.gameObject.AddComponent<BirdDrive>();
            drive.allowHeadless = true;
            drive.kind = birdDriveKind == "prey" ? BirdDrive.Kind.Prey : BirdDrive.Kind.Wander;
            drive.wanderSeed = birdDriveSeed;
            drive.sitMin = GetFloatArg("-sitMin", drive.sitMin);
            drive.sitMax = GetFloatArg("-sitMax", drive.sitMax);
            drive.flyMin = GetFloatArg("-flyMin", drive.flyMin);
            drive.flyMax = GetFloatArg("-flyMax", drive.flyMax);
            drive.glideLead = GetFloatArg("-glideLead", drive.glideLead);
            drive.leash = GetFloatArg("-flockLeash", drive.leash);
            drive.homeFaceDeadzone = GetFloatArg("-homeFaceDeadzone", drive.homeFaceDeadzone);
            drive.walkMin = GetFloatArg("-walkMin", drive.walkMin);
            drive.walkMax = GetFloatArg("-walkMax", drive.walkMax);
        }

        SoundBus.Clear();
        bool preyKind = birdDriveKind == "prey";
        float preyOff = GetFloatArg("-birdPrey", preyKind ? 10f : 0f);
        if (preyKind && Mathf.Abs(preyOff) < 0.01f)
            preyOff = 10f;
        if (preyKind || Mathf.Abs(preyOff) > 0.01f)
        {
            Transform preyMark = Insect.SpawnOnDirt(originX + preyOff, -2f);
            BindBirdPrey(pack, spawnedFlock, preyMark);
        }

        float threatOff = GetFloatArg("-birdThreat", 0f);
        if (Mathf.Abs(threatOff) > 0.01f)
        {
            GameObject mark = new GameObject("BirdThreat");
            mark.transform.position = new Vector2(originX + threatOff, startY);
            FactionStamp.Wolf(mark);
            float threatRange = GetFloatArg("-threatRange", 12f);
            BindBirdThreat(pack, spawnedFlock, mark.transform, threatRange);
        }
    }

    // Spawn already stamped Insects. Bind the live quarry without a scene scan.
    private static void BindBirdPrey(Bird[] pack, BirdFlockDrive flock, Transform prey)
    {
        if (prey == null)
            return;
        if (flock != null)
            flock.BindPrey(prey);
        if (pack == null)
            return;
        for (int i = 0; i < pack.Length; i++)
        {
            if (pack[i] == null)
                continue;
            BirdDrive drive = pack[i].GetComponent<BirdDrive>();
            if (drive != null)
                drive.BindPrey(i == 0 ? prey : null);
        }
    }

    // Same pack the spawn loop already holds. Not FindObjectsByType.
    private static void BindBirdThreat(
        Bird[] pack, BirdFlockDrive flock, Transform threat, float threatRange)
    {
        if (threat == null)
            return;
        if (flock != null)
        {
            flock.threat = threat;
            flock.threatRange = threatRange;
        }

        if (pack == null)
            return;
        for (int i = 0; i < pack.Length; i++)
        {
            if (pack[i] == null)
                continue;
            BirdDrive drive = pack[i].GetComponent<BirdDrive>();
            if (drive == null)
                continue;
            drive.threat = threat;
            drive.threatRange = threatRange;
        }
    }

    // Live quarry for DogDrive.ResolvePrey: inside noticeRange, on dirt,
    // facing the dog so BirdDrive.SeesAhead can Flee. Brains stay silent
    // on the stand unless allowHeadless is set here.
    private float DogHuntBirdOriginX()
    {
        float dogX = dog != null ? dog.transform.position.x : 0f;
        float notice = 8f;
        if (dog != null)
        {
            DogDrive drive = dog.GetComponent<DogDrive>();
            if (drive != null)
                notice = Mathf.Max(0.05f, drive.noticeRange);
        }

        float x = dogBirdX;
        if (Mathf.Abs(x) < 0.4f)
            x = 2f;
        float maxOff = notice * 0.75f;
        if (Mathf.Abs(x) > maxOff)
            x = Mathf.Sign(x) * maxOff;
        return dogX + x;
    }

    private void WireDogBirdHunt()
    {
        if (dog == null || bird == null)
            return;

        DogDrive dogDrive = dog.GetComponent<DogDrive>();
        if (dogDrive != null)
            dogDrive.allowHeadless = true;

        BirdDrive[] drives = FindObjectsByType<BirdDrive>(FindObjectsSortMode.None);
        if (drives == null || drives.Length == 0)
        {
            BirdDrive drive = bird.gameObject.AddComponent<BirdDrive>();
            drive.allowHeadless = true;
            drive.kind = BirdDrive.Kind.Wander;
            drive.wanderSeed = birdDriveSeed;
            drives = new[] { drive };
            if (bird.controller != null)
            {
                bird.controller.takeoffDelay = 0f;
                bird.controller.turnAfter = 0f;
                bird.controller.mode = BirdMode.Sit;
            }
        }

        for (int i = 0; i < drives.Length; i++)
        {
            if (drives[i] == null)
                continue;
            drives[i].allowHeadless = true;
            drives[i].threat = dog.transform;
        }

        BirdFlockDrive flockDrive = FindFirstObjectByType<BirdFlockDrive>();
        if (flockDrive != null)
            flockDrive.threat = dog.transform;

        if (!HasArg("-birdFacing") && bird.controller != null)
        {
            float dx = dog.transform.position.x - bird.transform.position.x;
            bird.controller.SetFacing(dx >= 0f ? 1f : -1f);
        }
    }

    private void BuildTreeScene()
    {
        GameObject treeObject = new GameObject("TrialTree");
        treeObject.transform.position = new Vector2(0f, PlantTree.RootY(-2f));
        plantTree = treeObject.AddComponent<PlantTree>();
        plantTree.kind = PlantTree.ParseKind(GetStringArg("-treeKind", "oak"));
        string treeRigRaw = GetStringArg("-treeRig", "sway").Trim().ToLowerInvariant();
        if (treeRigRaw == "static")
            plantTree.rig = TreeRig.Static;
        plantTree.seed = Mathf.RoundToInt(GetFloatArg("-treeSeed", 1f));
        plantTree.windScale = GetFloatArg("-treeWind", plantTree.windScale);
        // Round 14: the free stream and its gust are settable from the CLI; the
        // field switch (default 0) replaces both with the advected TreeWindField.
        plantTree.windSpeed = GetFloatArg("-treeWindSpeed", plantTree.windSpeed);
        plantTree.windGust = GetFloatArg("-treeGust", plantTree.windGust);
        plantTree.treeWindField = GetFloatArg("-treeWindField", plantTree.treeWindField);
        // T1-sway switch for the oak: 0 (default) keeps the sail on for the whole
        // trial, so every older oak run is unchanged by it.
        plantTree.windOffSeconds = GetFloatArg("-treeWindUntil", 0f);
        // Injected perch load: bend and return are measured with THIS, so a
        // broken bird layer can never make the tree look like it passes.
        plantTree.perchLoadKg = GetFloatArg("-treePerchLoad", 0f);
        plantTree.perchLoadSlot = Mathf.RoundToInt(GetFloatArg("-treePerchSlot", 0f));
        plantTree.perchLoadUntilSeconds = GetFloatArg("-treePerchLoadUntil", 0f);
        // treeseq (req-269): the bole as N dynamic hinge segments, its beam-law
        // modulus (0 = auto-tune at bind). Off = the folded static trunk.
        plantTree.treeSeg = Mathf.RoundToInt(GetFloatArg("-treeSeg", 0f));
        plantTree.treeSegModulusMPa = GetFloatArg("-treeSegE", 0f);
        // req-287 round 13 (TREES, delegation): the crown by the sway-margin rule,
        // the real stops (18/35/40) and the bole tuned on min eig(K+H). 0 = r12.
        plantTree.treeCrownMargin = GetFloatArg("-treeCrownMargin", 0f);
        plantTree.treeBoleMinEig = GetFloatArg("-treeBoleEig",
            GetFloatArg("-treeBoleMinEig", plantTree.treeBoleMinEig));
        // req-358 round 17: the floor of the crown margin (3 = the r16 x3 lump).
        plantTree.treeCrownFloor = GetFloatArg("-treeCrownFloor", plantTree.treeCrownFloor);
        // req-440 round 18: centre a wood stop on the joint's own rest angle (J11 pin).
        plantTree.treeJ11Fix = GetFloatArg("-treeJ11Fix", 0f);
        plantTree.treeJ11HeadroomDeg = GetFloatArg("-treeJ11Headroom", plantTree.treeJ11HeadroomDeg);
        // req-440 round 19: the leaf detach threshold in m/s of relative wind.
        plantTree.treeLeafDetach = GetFloatArg("-treeLeafDetach", 0f);
        // req-440 round 20: E on J00-J03 straight, MPa.
        plantTree.treeBoleE = GetFloatArg("-treeBoleE", 0f);
        // req-447 r21 (tall tree): every part size x s with the density kept; the
        // crown grows to depth n inside a cap; Leonardo's width decay 0.707 for a
        // two-way fork; a per-cluster strength spread so a gust takes a few clusters.
        plantTree.treeScale = GetFloatArg("-treeScale", 0f);
        plantTree.treeDepth = Mathf.RoundToInt(GetFloatArg("-treeDepth", 0f));
        plantTree.treeCap = Mathf.RoundToInt(GetFloatArg("-treeCap", 0f));
        plantTree.treeWidthDecay = GetFloatArg("-treeWidthDecay", 0f);
        plantTree.treeLeafProbe = GetFloatArg("-treeLeafProbe", 0f);
        plantTree.treeLeafSpread = GetFloatArg("-treeLeafSpread", 0f);
        // req-451 r22: the wood carries the load it sees. Every spring constant and
        // torque cap of the stem scales as the gravity load s^p (p = 4); 0 = off.
        plantTree.treeScaleStiff = GetFloatArg("-treeScaleStiff", 0f);
        plantTree.treeStiffExp = GetFloatArg("-treeStiffExp", 4f);
        // req-455 r23: the floor as a multiple of the joint's gravity gradient; 0 = 3 (rep-451).
        plantTree.treeStiffFloorK = GetFloatArg("-treeStiffFloorK", 0f);
        // req-455 r23 r2: the sag floor, static sag |holdTorque|/k under -treeWoodSagDeg.
        plantTree.treeWoodSagDeg = GetFloatArg("-treeWoodSagDeg", 0f);
        // req-459 r1: the wood margin ceiling, k <= m * Ieff / dt^2 on every wood joint.
        plantTree.treeMarginCeil = GetFloatArg("-treeMarginCeil", 0f);
        // Round 16: the bind-centred crown stop is opt-in; 0 keeps the r12 stop.
        plantTree.treeCrownStop = GetFloatArg("-treeCrownStop", 0f);
        // treelvs (req-269 round 7): one physical leaf cluster per twig tip plus
        // one inner per limb. No collider. Off = the picture-only leaves.
        plantTree.treeLeaves = HasArg("-treeLeaves");
        // The owner's landing sheet (25.09): the trigger ring at the branch
        // forks, on the branch segments and one at the trunk's top, plus the
        // bird's-crossing probe. Off by default, so every accepted oak run
        // keeps the trajectory it was accepted with.
        plantTree.landingTriggers = HasArg("-treeTriggers");
        plantTree.landingTriggerCap = Mathf.RoundToInt(
            GetFloatArg("-treeTriggerCap", plantTree.landingTriggerCap));
        plantTree.landingTriggerRadius = GetFloatArg("-treeTriggerRadius", plantTree.landingTriggerRadius);
        plantTree.loadOnLandingTrigger = HasArg("-treeLoadOnTrigger");
        plantTree.landingLoadSlot = Mathf.RoundToInt(GetFloatArg("-treeTriggerSlot", 0f));
        plantTree.BuildTree();
        if (HasArg("-treeProbe"))
            plantTree.landingProbe = TreeLandingProbe.Attach(plantTree, GetFloatArg("-treeProbeSpeed", 0f));
    }

    private void BuildAppleScene()
    {
        GameObject appleObject = new GameObject("TrialAppleTree");
        appleObject.transform.position = new Vector2(0f, PlantBase.RootY(-2f));
        appleTree = appleObject.AddComponent<AppleTree>();
        appleTree.seed = Mathf.RoundToInt(GetFloatArg("-appleSeed", 1f));
        float forcedHeight = GetFloatArg("-appleHeight", 0f);
        if (forcedHeight > 0f)
            appleTree.appleHeightM = forcedHeight;
        appleTree.windScale = GetFloatArg("-appleWind", appleTree.windScale);
        appleTree.windSpeed = GetFloatArg("-appleWindSpeed", appleTree.windSpeed);
        // The wind-off decay run: 0 (default) keeps the sail on for the whole
        // trial, so every older apple run is unchanged by this switch.
        appleTree.windOffSeconds = GetFloatArg("-appleWindUntil", 0f);
        appleTree.Build();
    }

    private void SeatTrialBirdsOnTree()
    {
        Bird[] pack = trialBirds != null && trialBirds.Length > 0
            ? trialBirds
            : bird != null ? new[] { bird } : System.Array.Empty<Bird>();
        PlantTree.SeatBirds(pack, plantTree);
        Vector2[] slots = plantTree.GetPerchSlots();
        BirdFlockDrive flock = FindFirstObjectByType<BirdFlockDrive>();
        if (flock != null)
            flock.BindPerches(slots);
        else if (bird != null && slots.Length > 0)
        {
            BirdDrive drive = bird.GetComponent<BirdDrive>();
            if (drive != null)
                drive.BindPerch(slots[0]);
        }
    }

    private IEnumerator RunTreeTrial()
    {
        plantRecorder = new PlantTrialRecorder(plantTree, runInfo);
        float trialWallStart = Time.realtimeSinceStartup;
        float simulated = 0f;
        float realDeadline = Time.realtimeSinceStartup + REAL_TIME_LIMIT_SECONDS;
        int lastShot = -1;
        while (simulated < duration)
        {
            yield return FixedStep;
            simulated += Time.fixedDeltaTime;
            plantRecorder.Sample(simulated);

            // Same capture as the human, bird and dog trials. Without it every
            // tree acceptance was a number only and no one ever looked at a
            // tree. A plant change is accepted by a picture.
            if (visual)
            {
                int shotIdx = (int)(simulated / shotPeriod);
                if (shotIdx != lastShot)
                {
                    lastShot = shotIdx;
                    string shotPath = Path.Combine(outputFolder, "shot_" + label + "_" + shotIdx.ToString("00") + ".png");
                    yield return CaptureVisualFrame(shotPath);
                }
            }

            if (Time.realtimeSinceStartup > realDeadline)
            {
                Debug.LogWarning("HeadlessTrial: real-time limit exceeded, run aborted.");
                break;
            }
        }

        Finish(simulated, Time.realtimeSinceStartup - trialWallStart);
    }

    private IEnumerator RunAppleTrial()
    {
        appleRecorder = new AppleTrialRecorder(appleTree, runInfo);
        float trialWallStart = Time.realtimeSinceStartup;
        float simulated = 0f;
        float realDeadline = Time.realtimeSinceStartup + REAL_TIME_LIMIT_SECONDS;
        int lastShot = -1;
        while (simulated < duration)
        {
            yield return FixedStep;
            simulated += Time.fixedDeltaTime;
            appleRecorder.Sample(simulated);
            if (visual)
            {
                int shotIdx = (int)(simulated / shotPeriod);
                if (shotIdx != lastShot)
                {
                    lastShot = shotIdx;
                    string shotPath = Path.Combine(outputFolder, "shot_" + label + "_" + shotIdx.ToString("00") + ".png");
                    yield return CaptureVisualFrame(shotPath);
                }
            }
            if (Time.realtimeSinceStartup > realDeadline)
            {
                Debug.LogWarning("HeadlessTrial: real-time limit exceeded, run aborted.");
                break;
            }
        }

        Finish(simulated, Time.realtimeSinceStartup - trialWallStart);
    }

    // Picture-only garden plants. No hinges. Default row is all 12 kinds.
    private void BuildFlowerScene()
    {
        string kindRaw = GetStringArg("-flowerKind", "");
        bool lockKind = !string.IsNullOrEmpty(kindRaw);
        FlowerKind locked = lockKind ? PlantFlower.ParseKind(kindRaw) : FlowerKind.Rose;
        int count = Mathf.Max(1, Mathf.RoundToInt(GetFloatArg("-flowerCount", lockKind ? 1f : 12f)));
        float spacing = GetFloatArg("-flowerSpacing", 0.55f);
        float originX = GetFloatArg("-flowerOffsetX", -3.3f);
        trialFlowers = new PlantFlower[count];
        int seed0 = Mathf.RoundToInt(GetFloatArg("-flowerSeed", 1f));
        for (int i = 0; i < count; i++)
        {
            FlowerKind kind = lockKind ? locked : PlantFlower.KindFromIndex(i);
            GameObject go = new GameObject("TrialFlower_" + (i + 1) + "_" + kind);
            go.transform.position = new Vector2(originX + i * spacing, PlantFlower.RootY(-2f));
            PlantFlower flower = (PlantFlower)go.AddComponent(PlantFlower.ComponentType(kind));
            flower.kind = kind;
            flower.seed = seed0 + i;
            flower.BuildFlower();
            trialFlowers[i] = flower;
        }
        flowerRecorder = new FlowerTrialRecorder(trialFlowers, runInfo);
    }

    private IEnumerator RunFlowerTrial()
    {
        if (flowerRecorder == null)
            flowerRecorder = new FlowerTrialRecorder(trialFlowers, runInfo);
        float trialWallStart = Time.realtimeSinceStartup;
        float simulated = 0f;
        float realDeadline = Time.realtimeSinceStartup + REAL_TIME_LIMIT_SECONDS;
        while (simulated < duration)
        {
            yield return FixedStep;
            simulated += Time.fixedDeltaTime;
            if (Time.realtimeSinceStartup > realDeadline)
            {
                Debug.LogWarning("HeadlessTrial: real-time limit exceeded, run aborted.");
                break;
            }
        }

        Finish(simulated, Time.realtimeSinceStartup - trialWallStart);
    }

    private void ApplyBirdGains(BirdController c)
    {
        if (c == null)
        {
            Debug.LogError("HeadlessTrial: BirdController not found.");
            return;
        }

        string modeRaw = GetStringArg("-birdMode", "stand").Trim().ToLowerInvariant();
        if (modeRaw == "fly") c.mode = BirdMode.Fly;
        else if (modeRaw == "walk") c.mode = BirdMode.Walk;
        else if (modeRaw == "glide") c.mode = BirdMode.Glide;
        else if (modeRaw == "sit") c.mode = BirdMode.Sit;
        else if (modeRaw == "attack") c.mode = BirdMode.Attack;
        else c.mode = BirdMode.Stand;

        c.takeoffDelay = GetFloatArg("-takeoffDelay", 0f);
        c.SetFacing(GetFloatArg("-birdFacing", 1f));
        c.turnAfter = GetFloatArg("-birdTurnAfter", 0f);
        c.flapFrequency = GetFloatArg("-flapHz", c.flapFrequency);
        c.flapAmplitude = GetFloatArg("-flapAmp", c.flapAmplitude);
        c.flapStrokeGain = GetFloatArg("-flapStroke", c.flapStrokeGain);
        c.flapMidAngle = GetFloatArg("-flapMid", c.flapMidAngle);
        c.jointPGain = GetFloatArg("-jointP", c.jointPGain);
        c.jointDGain = GetFloatArg("-jointD", c.jointDGain);
        c.pitchPGain = GetFloatArg("-pitchP", c.pitchPGain);
        c.pitchDGain = GetFloatArg("-pitchD", c.pitchDGain);
        c.pitchTarget = GetFloatArg("-pitchTarget", c.pitchTarget);
        c.ankleComP = GetFloatArg("-ankleComP", c.ankleComP);
        c.ankleComD = GetFloatArg("-ankleComD", c.ankleComD);
        c.standHipAngle = GetFloatArg("-standHip", c.standHipAngle);
        c.standKneeAngle = GetFloatArg("-standKnee", c.standKneeAngle);
        c.walkThrust = GetFloatArg("-walkThrust", c.walkThrust);
        c.walkFrequency = GetFloatArg("-walkHz", c.walkFrequency);
        // B-PERCH-23: the peck pose's own probes (the standing values are the defaults).
        c.peckDipDegrees = GetFloatArg("-peckDip", c.peckDipDegrees);
        c.peckReachAhead = GetFloatArg("-peckReachAhead", c.peckReachAhead);
    }

    private void ApplyMultipliers(Human target, float muscle, float friction)
    {
        target.muscleMultiplier = muscle;
        target.frictionMultiplier = friction;

        target.hipMuscleTorque *= muscle;
        target.kneeMuscleTorque *= muscle;
        target.ankleExtensorTorque *= muscle;
        target.ankleFlexorTorque *= muscle;
        target.shoulderMuscleTorque *= muscle;
        target.elbowMuscleTorque *= muscle;
        target.wristMuscleTorque *= muscle;
        target.toeMuscleTorque *= muscle;
        target.neckMuscleTorque *= muscle;
        target.lumbarMuscleTorque *= muscle;

        target.hipFriction *= friction;
        target.kneeFriction *= friction;
        target.ankleFriction *= friction;
        target.shoulderFriction *= friction;
        target.elbowFriction *= friction;
        target.wristFriction *= friction;
        target.toeFriction *= friction;
        target.neckFriction *= friction;
        target.lumbarFriction *= friction;
        target.lumbarFrictionMaxTorque = GetFloatArg("-lumbarFrictionCap", target.lumbarFrictionMaxTorque);
    }

    // Overrides only those K that were passed explicitly on the CLI.
    // Neck and head share one neckFriction field — there is no separate head flag.
    private static void ApplyFrictionOverrides(Human target)
    {
        if (target == null) return;
        target.neckFriction = GetFloatArg("-neckFriction", target.neckFriction);
        target.shoulderFriction = GetFloatArg("-shoulderFriction", target.shoulderFriction);
        target.elbowFriction = GetFloatArg("-elbowFriction", target.elbowFriction);
        target.wristFriction = GetFloatArg("-wristFriction", target.wristFriction);
        target.toeFriction = GetFloatArg("-toeFriction", target.toeFriction);
    }

    // Neck and head muscle torque: absolute value, after the -muscle multiplier.
    // One field for both joints, same as in Human. It needs its own flag because
    // at 0.0015 kg·m² inertia the old 15 N·m gave 9900 rad/s², and PD
    // reversed the joint every physics step.
    private static void ApplyMuscleOverrides(Human target)
    {
        if (target == null) return;
        target.neckMuscleTorque = GetFloatArg("-neckMuscle", target.neckMuscleTorque);
        target.toeMuscleTorque = GetFloatArg("-toeTorque", target.toeMuscleTorque);
    }

    private void ApplyGains(BalanceController balance)
    {
        if (balance == null)
        {
            Debug.LogError("HeadlessTrial: BalanceController not found on the human.");
            return;
        }

        balance.hipPGain = GetFloatArg("-hipP", balance.hipPGain);
        balance.hipDGain = GetFloatArg("-hipD", balance.hipDGain);
        // Quiet-stance anti-split mix. 0 = former pelvis-only hips.
        // Walk / one-leg already skip the mix in ControlHipsToWorldUpright.
        balance.hipHoldBand = GetFloatArg("-hipHoldBand", balance.hipHoldBand);
        balance.hipHoldAngle = GetFloatArg("-hipHoldAngle", balance.hipHoldAngle);
        balance.kneePGain = GetFloatArg("-kneeP", balance.kneePGain);
        balance.kneeDGain = GetFloatArg("-kneeD", balance.kneeDGain);
        balance.comProportionalGain = GetFloatArg("-comP", balance.comProportionalGain);
        balance.comDerivativeGain = GetFloatArg("-comD", balance.comDerivativeGain);

        balance.hipBaseAngle = GetFloatArg("-hipBase", balance.hipBaseAngle);
        balance.kneeBaseAngle = GetFloatArg("-kneeBase", balance.kneeBaseAngle);
        balance.hipBalanceGain = GetFloatArg("-hipBalance", balance.hipBalanceGain);
        balance.ankleComP = GetFloatArg("-ankleComP", balance.ankleComP);
        balance.ankleComD = GetFloatArg("-ankleComD", balance.ankleComD);
        balance.lumbarPGain = GetFloatArg("-lumbarP", balance.lumbarPGain);
        balance.lumbarDGain = GetFloatArg("-lumbarD", balance.lumbarDGain);
        balance.lumbarTargetTilt = GetFloatArg("-lumbarTilt", balance.lumbarTargetTilt);
        balance.lumbarErrorReferenceDegrees = GetFloatArg("-lumbarRef", balance.lumbarErrorReferenceDegrees);
        balance.recoveryCoMOffset = GetFloatArg("-recoveryOffset", balance.recoveryCoMOffset);
        balance.fallCoMOffset = GetFloatArg("-fallOffset", balance.fallCoMOffset);
        // c99 AUTO RISE (the owner's addendum 01.10): a keyless flat heap held
        // this many seconds makes the man try to rise himself. 0 = off.
        balance.autoGetUpDwell = GetFloatArg("-autoGetUp", balance.autoGetUpDwell);
        balance.crouchRatePerSecond = GetFloatArg("-crouchRate", balance.crouchRatePerSecond);
        balance.crouchReleaseRatePerSecond = GetFloatArg("-crouchReleaseRate", balance.crouchReleaseRatePerSecond);
        balance.crouchKneeFlex = GetFloatArg("-crouchKnee", balance.crouchKneeFlex);
        balance.crouchHipFlex = GetFloatArg("-crouchHip", balance.crouchHipFlex);
        balance.crouchPelvisTilt = GetFloatArg("-crouchPelvis", balance.crouchPelvisTilt);
        balance.crouchTorsoLean = GetFloatArg("-crouchLean", balance.crouchTorsoLean);
        balance.leanRatePerSecond = GetFloatArg("-leanRate", balance.leanRatePerSecond);
        balance.leanTorsoAngle = GetFloatArg("-leanTorso", balance.leanTorsoAngle);
        balance.leanPelvisTilt = GetFloatArg("-leanPelvis", balance.leanPelvisTilt);
        balance.crouchArmShoulder = GetFloatArg("-crouchArmShoulder", balance.crouchArmShoulder);
        balance.crouchArmElbow = GetFloatArg("-crouchArmElbow", balance.crouchArmElbow);
        balance.crouchArmWrist = GetFloatArg("-crouchArmWrist", balance.crouchArmWrist);
        balance.crouchHandSupportMin = GetFloatArg("-crouchHandMin", balance.crouchHandSupportMin);
        balance.crouchHandGroundSlop = GetFloatArg("-crouchHandSlop", balance.crouchHandGroundSlop);
        balance.crouchHandPoseStart = GetFloatArg("-crouchHandPoseStart", balance.crouchHandPoseStart);
        balance.crouchHandSupportShoulder = GetFloatArg("-crouchHandShoulder", balance.crouchHandSupportShoulder);
        balance.crouchHandSupportElbow = GetFloatArg("-crouchHandElbow", balance.crouchHandSupportElbow);
        balance.crouchHandSupportWrist = GetFloatArg("-crouchHandWrist", balance.crouchHandSupportWrist);
        balance.crouchHandSupportSpread = GetFloatArg("-crouchHandSpread", balance.crouchHandSupportSpread);
        balance.crouchHandBalanceSpread = GetFloatArg("-crouchHandBalSpread", balance.crouchHandBalanceSpread);
        balance.pelvisPGain = GetFloatArg("-pelvisP", balance.pelvisPGain);
        balance.pelvisDGain = GetFloatArg("-pelvisD", balance.pelvisDGain);
        balance.pelvisErrorReferenceDegrees = GetFloatArg("-pelvisRef", balance.pelvisErrorReferenceDegrees);
        balance.neckPGain = GetFloatArg("-neckP", balance.neckPGain);
        balance.neckDGain = GetFloatArg("-neckD", balance.neckDGain);
        // LANDING TRANSIENT (drop test, line 3, 27.09): same knobs on the CLI as
        // in the inspector, so one build can sweep the landing phase instead of
        // a rebuild per value. Defaults are the field values (see
        // BalanceController); -landing 0 turns the whole phase off.
        balance.landingTransient = GetFloatArg("-landing", balance.landingTransient ? 1f : 0f) > 0.5f;
        balance.landingPrepDescent = GetFloatArg("-landingPrepDescent", balance.landingPrepDescent);
        balance.landingPrepAirMin = GetFloatArg("-landingAirMin", balance.landingPrepAirMin);
        balance.landingPrepRamp = GetFloatArg("-landingPrepRamp", balance.landingPrepRamp);
        balance.landingPrepKneeFlex = GetFloatArg("-landingPrepKnee", balance.landingPrepKneeFlex);
        balance.landingPrepAnkleGain = GetFloatArg("-landingFlatGain", balance.landingPrepAnkleGain);
        balance.landingYieldKneeFlex = GetFloatArg("-landingYieldKnee", balance.landingYieldKneeFlex);
        balance.landingYieldTime = GetFloatArg("-landingYieldTime", balance.landingYieldTime);
        balance.landingYieldRiseFrac = GetFloatArg("-landingYieldRise", balance.landingYieldRiseFrac);
        balance.landingYieldImpactRef = GetFloatArg("-landingImpactRef", balance.landingYieldImpactRef);
        balance.landingImpactCap = GetFloatArg("-landingImpactCap", balance.landingImpactCap);
        balance.landingCatchBand = GetFloatArg("-landingCatchBand", balance.landingCatchBand);
        balance.landingCatchHipGain = GetFloatArg("-landingCatchGain", balance.landingCatchHipGain);
        balance.landingCatchHipMax = GetFloatArg("-landingCatchMax", balance.landingCatchHipMax);
        balance.landingCatchLeadTime = GetFloatArg("-landingCatchLead", balance.landingCatchLeadTime);
        balance.landingCatchMaxTime = GetFloatArg("-landingCatchTime", balance.landingCatchMaxTime);
        balance.landingCatchToeGain = GetFloatArg("-landingCatchToeGain", balance.landingCatchToeGain);
        balance.landingCatchToeMax = GetFloatArg("-landingCatchToeMax", balance.landingCatchToeMax);
        balance.landingPrepPelvisDeg = GetFloatArg("-landingPrepPelvis", balance.landingPrepPelvisDeg);
        // The per-plate ankle split (VEST-2) is the reason one plate at a time
        // carries the CoM push; exposed here so a landing run can measure the
        // pair against the shared loop without a rebuild.
        balance.quietAnklePerFoot = GetFloatArg("-quietAnklePerFoot", balance.quietAnklePerFoot);
        balance.neckTargetTilt = GetFloatArg("-neckTilt", balance.neckTargetTilt);
        balance.neckErrorReferenceDegrees = GetFloatArg("-neckRef", balance.neckErrorReferenceDegrees);
        balance.useStablePd = GetFloatArg("-spd", balance.useStablePd ? 1f : 0f) > 0.5f;
        balance.shoulderPGain = GetFloatArg("-shoulderP", balance.shoulderPGain);
        balance.shoulderDGain = GetFloatArg("-shoulderD", balance.shoulderDGain);
        balance.shoulderBaseAngle = GetFloatArg("-shoulderBase", balance.shoulderBaseAngle);
        // b7 (ball line): per-side shoulder base for the standing pose; NaN = follow
        // -shoulderBase (the shipped single knob). Asymmetric poses for the hail's
        // shoulder channels.
        balance.shoulderBaseL = GetFloatArg("-shoulderBaseL", balance.shoulderBaseL);
        balance.shoulderBaseR = GetFloatArg("-shoulderBaseR", balance.shoulderBaseR);
        balance.shoulderErrorReferenceDegrees = GetFloatArg("-shoulderRef", balance.shoulderErrorReferenceDegrees);
        balance.elbowPGain = GetFloatArg("-elbowP", balance.elbowPGain);
        balance.elbowDGain = GetFloatArg("-elbowD", balance.elbowDGain);
        balance.elbowBaseAngle = GetFloatArg("-elbowBase", balance.elbowBaseAngle);
        balance.elbowErrorReferenceDegrees = GetFloatArg("-elbowRef", balance.elbowErrorReferenceDegrees);
        balance.wristPGain = GetFloatArg("-wristP", balance.wristPGain);
        balance.wristDGain = GetFloatArg("-wristD", balance.wristDGain);
        balance.wristBaseAngle = GetFloatArg("-wristBase", balance.wristBaseAngle);
        balance.wristErrorReferenceDegrees = GetFloatArg("-wristRef", balance.wristErrorReferenceDegrees);
        balance.armShoulderBalanceGain = GetFloatArg("-armShoulderBal", balance.armShoulderBalanceGain);
        balance.armElbowBalanceGain = GetFloatArg("-armElbowBal", balance.armElbowBalanceGain);
        balance.armWristBalanceGain = GetFloatArg("-armWristBal", balance.armWristBalanceGain);
        balance.swingHipFlex = GetFloatArg("-swingHip", balance.swingHipFlex);
        balance.swingKneeFlex = GetFloatArg("-swingKnee", balance.swingKneeFlex);
        balance.marchHipFlex = GetFloatArg("-marchHip", balance.marchHipFlex);
        balance.marchKneeFlex = GetFloatArg("-marchKnee", balance.marchKneeFlex);
        balance.marchPelvisHold = GetFloatArg("-marchPelvisHold", balance.marchPelvisHold);
        balance.marchComPelvisGain = GetFloatArg("-marchPelvisCom", balance.marchComPelvisGain);
        balance.marchPelvisComClamp = GetFloatArg("-marchPelvisClamp", balance.marchPelvisComClamp);
        balance.marchAnkleRefScale = GetFloatArg("-marchAnkleRef", balance.marchAnkleRefScale);
        balance.marchStancePelvisP = GetFloatArg("-marchStanceP", balance.marchStancePelvisP);
        balance.marchAnkleCap = GetFloatArg("-marchAnkleCap", balance.marchAnkleCap);
        balance.marchAnkleToeGate = GetFloatArg("-marchAnkleToe", balance.marchAnkleToeGate);
        balance.marchAnkleHeelGate = GetFloatArg("-marchAnkleHeel", balance.marchAnkleHeelGate);
        balance.marchComDelay = GetFloatArg("-marchComDelay", balance.marchComDelay);
        balance.marchPoseHip = GetFloatArg("-marchPoseHip", balance.marchPoseHip);
        balance.marchPoseKnee = GetFloatArg("-marchPoseKnee", balance.marchPoseKnee);
        balance.marchPrepareTilt = GetFloatArg("-marchPrepare", balance.marchPrepareTilt);
        balance.marchLiftTilt = GetFloatArg("-marchLift", balance.marchLiftTilt);
        balance.marchTiltRate = GetFloatArg("-marchTiltRate", balance.marchTiltRate);
        balance.swingHipUnloadBias = GetFloatArg("-swingHipUnload", balance.swingHipUnloadBias);
        balance.swingKneeGroundedFraction = GetFloatArg("-swingKneeGround", balance.swingKneeGroundedFraction);
        balance.forwardSwingHipUnloadScale = GetFloatArg("-forwardUnloadScale", balance.forwardSwingHipUnloadScale);
        balance.forwardSwingKneeGroundScale = GetFloatArg("-forwardKneeScale", balance.forwardSwingKneeGroundScale);
        balance.backSwingHipUnloadScale = GetFloatArg("-backUnloadScale", balance.backSwingHipUnloadScale);
        balance.backSwingKneeGroundScale = GetFloatArg("-backKneeScale", balance.backSwingKneeGroundScale);
        balance.backPelvisTiltFraction = GetFloatArg("-backPelvisTilt", balance.backPelvisTiltFraction);
        balance.swingAnkleGroundedToeOff = GetFloatArg("-swingAnkleToeOff", balance.swingAnkleGroundedToeOff);
        balance.standLegPelvisTilt = GetFloatArg("-standPelvisTilt", balance.standLegPelvisTilt);
        balance.oneLegPelvisPBoost = GetFloatArg("-oneLegPelvisP", balance.oneLegPelvisPBoost);
        balance.oneLegPelvisDBoost = GetFloatArg("-oneLegPelvisD", balance.oneLegPelvisDBoost);
        // Same tilt on right support during fall-catch walk. 0 = former
        // left-support-only tilt, bit for bit.
        balance.walkStandLegPelvisTiltMirror = GetFloatArg(
            "-walkStandTiltMirror", balance.walkStandLegPelvisTiltMirror);
        balance.dualSupportSwingScale = GetFloatArg("-dualSupportSwing", balance.dualSupportSwingScale);
        balance.dualSupportStandCap = GetFloatArg("-dualSupportCap", balance.dualSupportStandCap);
        balance.walkAirReleaseAuthority = GetFloatArg("-walkAirRelease", balance.walkAirReleaseAuthority);
        // Same air release on the pelvis-tilt softener. 0 = the former frozen
        // dualSupportSwingScale floor on the tilt, bit for bit.
        balance.walkStandTiltAirRelease = GetFloatArg(
            "-walkStandTiltAir", balance.walkStandTiltAirRelease);
        // Measured double-support weight share on the same ramp. 0 = the
        // former clearance-only release, bit for bit.
        balance.walkDualLoadRelease = GetFloatArg(
            "-walkDualLoadRelease", balance.walkDualLoadRelease);
        // Measured leading/trailing share on stanceLoad / Plant rear hip
        // and the Plant trailing-ankle A2 burst. -walkStanceLoadShare 0
        // restores the former 0.5 dual-support floor and the unscaled A2.
        balance.walkStanceLoadShare = GetFloatArg(
            "-walkStanceLoadShare", balance.walkStanceLoadShare);
        balance.walkReachAirSwingSeconds = GetFloatArg("-walkReachAirSwing", balance.walkReachAirSwingSeconds);
        balance.walkSwingLiftScale = GetFloatArg("-walkSwingLift", balance.walkSwingLiftScale);
        balance.walkStanceLiftDelay = GetFloatArg("-walkLiftDelay", balance.walkStanceLiftDelay);
        balance.walkStanceLiftRamp = GetFloatArg("-walkLiftRamp", balance.walkStanceLiftRamp);
        balance.walkWeightRamp = GetFloatArg("-walkWeightRamp", balance.walkWeightRamp);
        balance.runStanceLiftDelay = GetFloatArg("-runLiftDelay", balance.runStanceLiftDelay);
        balance.runWeightRamp = GetFloatArg("-runWeightRamp", balance.runWeightRamp);
        balance.walkLiftComMax = GetFloatArg("-walkLiftCom", balance.walkLiftComMax);
        balance.walkSwingToeOffMax = GetFloatArg("-walkSwingToeOff", balance.walkSwingToeOffMax);
        balance.walkLiftLevelRateScale = GetFloatArg("-walkLiftLevelRate", balance.walkLiftLevelRateScale);
        balance.walkLiftUnloadBias = GetFloatArg("-walkLiftUnload", balance.walkLiftUnloadBias);
        balance.walkKneePeelMax = GetFloatArg("-walkKneePeel", balance.walkKneePeelMax);
        balance.walkStancePush = GetFloatArg("-walkStancePush", balance.walkStancePush);
        balance.runStancePushScale = GetFloatArg("-runStancePushScale", balance.runStancePushScale);
        balance.walkStanceHipExtend = GetFloatArg("-walkStanceHipExtend", balance.walkStanceHipExtend);
        balance.walkStanceHipExtendMax = GetFloatArg("-walkStanceHipExtendMax", balance.walkStanceHipExtendMax);
        // Stance push-off once CoM has passed the stance foot (fall-catch
        // and WALK-1 Stance after geo-lift). -walkStancePushOnLead 0
        // restores the former airborne-only gate.
        balance.walkStancePushOnLead = GetFloatArg(
            "-walkStancePushOnLead", balance.walkStancePushOnLead);
        balance.walkPlantPushScale = GetFloatArg("-walkPlantPush", balance.walkPlantPushScale);
        balance.walkPlantPushLeadWindow = GetFloatArg("-walkPlantPushLead", balance.walkPlantPushLeadWindow);
        // Plant roll-over setpoint. -walkPlantRock 0 restores the former
        // hold-the-CoM-on-the-foot Plant (release on plantLeadWait alone).
        balance.walkPlantRockLead = GetFloatArg("-walkPlantRock", balance.walkPlantRockLead);
        // Trunk half of that roll-over. -walkPlantLean 0 restores the former
        // upright / braked Plant, bit for bit.
        balance.walkPlantLeanRoll = GetFloatArg("-walkPlantLean", balance.walkPlantLeanRoll);
        // Same lean on cycle 0. -walkPlantLeanFirst 0 restores the former
        // later-cycle-only gate (feet-together first step had rock lead only).
        balance.walkPlantLeanFirst = GetFloatArg(
            "-walkPlantLeanFirst", balance.walkPlantLeanFirst);
        // Hip half of the Plant push-off, as a fraction of walkStanceHipExtend.
        // -walkPlantHipExtend 0 restores the former idle Plant hip, bit for bit.
        balance.walkPlantHipExtend = GetFloatArg(
            "-walkPlantHipExtend", balance.walkPlantHipExtend);
        // Support identity through the walk-release window.
        // -walkEndHoldSupport 0 restores the former instant drop, bit for bit.
        balance.walkEndHoldSupport = GetFloatArg(
            "-walkEndHoldSupport", balance.walkEndHoldSupport);
        // Plant trailing-knee lift once the CoM has rolled off that foot.
        // -walkPlantSwingKnee 0 restores the former Plant with no swing fold.
        balance.walkPlantSwingKnee = GetFloatArg(
            "-walkPlantSwingKnee", balance.walkPlantSwingKnee);
        // Remap that fold across the remaining half-span. 0 = 22:00
        // swingKneeFlex × clock (27.5° step at mid-span, full thighHold,
        // DrivePelvis drop at reachKnee > 0.5°).
        balance.walkPlantKneeRamp = GetFloatArg(
            "-walkPlantKneeRamp", balance.walkPlantKneeRamp);
        // Same Plant swing-knee fold on cycle 0. -walkPlantSwingKneeFirst 0
        // restores the former later-cycle-only gate (feet-together first
        // step had lean / rock lead and a straight trailing shin).
        balance.walkPlantSwingKneeFirst = GetFloatArg(
            "-walkPlantSwingKneeFirst", balance.walkPlantSwingKneeFirst);
        // Same Plant stance-hip drive on cycle 0. -walkPlantStanceDriveFirst 0
        // restores the former wait-for-lead (pushAfterAir × pushWindow).
        balance.walkPlantStanceDriveFirst = GetFloatArg(
            "-walkPlantStanceDriveFirst", balance.walkPlantStanceDriveFirst);
        // Same Plant swing-leg viscosity fade on cycle 0. -walkPlantSwingFrictionFirst 0
        // restores the former DualLoad-only weld (the 03:45 fold command
        // fought stopTorque 15 N·m until a millimetre of air).
        balance.walkPlantSwingFrictionFirst = GetFloatArg(
            "-walkPlantSwingFrictionFirst", balance.walkPlantSwingFrictionFirst);
        // Same Plant stance-knee extra-bend fade on cycle 0.
        // -walkPlantStanceStraightFirst 0 restores the former DualLoad-only
        // 5° shuffle flex (liftGateBlend stays 0 while the feet sit under
        // the hip, so LiftFade never starts).
        balance.walkPlantStanceStraightFirst = GetFloatArg(
            "-walkPlantStanceStraightFirst", balance.walkPlantStanceStraightFirst);
        // Same Plant swing-hip flexor unload on cycle 0.
        // -walkPlantSwingUnloadFirst 0 restores the former DualLoad-only
        // 0.041 bias (liftGateBlend stays 0 while the feet sit under the
        // hip, so unloadSoft × standLegLevel never leaves the 0.32 × 0.40
        // floor). DrivePelvis stays; this is not ThighHold.
        balance.walkPlantSwingUnloadFirst = GetFloatArg(
            "-walkPlantSwingUnloadFirst", balance.walkPlantSwingUnloadFirst);
        // Same Plant pelvis-tilt unload on cycle 0. -walkPlantTiltFirst 0
        // restores the former DualLoad-only 1.28° (liftGateBlend stays 0
        // while the feet sit under the hip, so tiltSoft × standLegLevel
        // never leaves the 0.32 × 0.40 floor). DrivePelvis stays; this is
        // that loop's target, not ThighHold.
        balance.walkPlantTiltFirst = GetFloatArg(
            "-walkPlantTiltFirst", balance.walkPlantTiltFirst);
        // Same Plant swing-hip ThighHold on cycle 0. -walkPlantHipReleaseFirst 0
        // restores the former DrivePelvis lock (plantHipRelease stays 0 while
        // the feet sit under the hip, so reachKneeFolding never opens and
        // hipLift / thighHold are stored but never applied). This is the
        // later-cycle plantFold hand-off, not −swingHipFlex.
        balance.walkPlantHipReleaseFirst = GetFloatArg(
            "-walkPlantHipReleaseFirst", balance.walkPlantHipReleaseFirst);
        // Same Plant swing-ankle CoM release on cycle 0. -walkPlantSwingAnkleReleaseFirst 0
        // restores the former dual-support CoM hold on both soles (liftBlend
        // stays 0 on fall-catch, DualLoadMinSpan never opens while the feet
        // sit under the hip, so the assigned swing ankle cancels rock lead /
        // stance-hip / lean). Not trailing A2 and not an ankle-angle PD.
        balance.walkPlantSwingAnkleReleaseFirst = GetFloatArg(
            "-walkPlantSwingAnkleReleaseFirst", balance.walkPlantSwingAnkleReleaseFirst);
        // Same Plant released swing-ankle through the ankleLift overwrite.
        // -walkPlantSwingAnkleKeepFirst 0 restores the former CoM overwrite
        // once plantFold writes ankleLift (the swing sole's own loop puts
        // plantarflex back on the stacked foot and cancels rock lead /
        // stance-hip / lean). Dorsiflex unweight stays; this is not trailing
        // A2 and not an ankle-angle PD.
        balance.walkPlantSwingAnkleKeepFirst = GetFloatArg(
            "-walkPlantSwingAnkleKeepFirst", balance.walkPlantSwingAnkleKeepFirst);
        // Same Plant released swing-ankle through Plant→Fall / grounded Reach.
        // -walkPlantFallAnkleKeepFirst 0 restores the former CoM overwrite
        // plus propulsive-half clamp on the first Fall frame (stacked soles
        // have no trailing arm, so that clamp deletes dorsiflex and ankleLift
        // mix from zero stays zero). Dorsiflex unweight stays; this is not
        // trailing A2 and not an ankle-angle PD. Catch / later cycles keep
        // the overwrite.
        balance.walkPlantFallAnkleKeepFirst = GetFloatArg(
            "-walkPlantFallAnkleKeepFirst", balance.walkPlantFallAnkleKeepFirst);
        // Same Fall keep, but hold the Plant release endpoint (zero) instead
        // of dual-support CoM restored by the PhaseAge reset. -walkPlantFallAnkleReleaseHoldFirst 0
        // restores the former keep of leftAnkleBalance (midpoint loop on
        // stacked soles, which cancels rock lead / stance-hip / lean).
        balance.walkPlantFallAnkleReleaseHoldFirst = GetFloatArg(
            "-walkPlantFallAnkleReleaseHoldFirst", balance.walkPlantFallAnkleReleaseHoldFirst);
        // Same Fall keep of the Plant swing-leg viscosity fade. EnterFall
        // resets PhaseAge, DualLoadMinSpan never opens, airRelease is 0 on
        // dirt, so loadFade is 0 and ApplySwingLegFriction(0) restores the
        // weld on the shin walkPlantFallFoldHold is holding at 55°.
        // -walkPlantFallFrictionHoldFirst 0 restores that weld on the first
        // Fall/Reach frame. Catch / later cycles keep DualLoad / airRelease.
        balance.walkPlantFallFrictionHoldFirst = GetFloatArg(
            "-walkPlantFallFrictionHoldFirst", balance.walkPlantFallFrictionHoldFirst);
        // Same Fall keep of the Plant stance-hip drive. EnterFall resets
        // PhaseAge, DualLoadMinSpan never opens, the swing has never left
        // so pushAfterAir is 0, CoM sits on the stacked feet so pushWindow
        // is 0, and the later-cycle extend is 0: the stance hip is
        // world-upright again on the phase whose job is the lift.
        // -walkPlantFallStanceDriveHoldFirst 0 restores that world-upright
        // stance hip on the first Fall/Reach frame. Catch / later cycles
        // keep pushAfterAir / pushWindow. Not trailing A2.
        balance.walkPlantFallStanceDriveHoldFirst = GetFloatArg(
            "-walkPlantFallStanceDriveHoldFirst", balance.walkPlantFallStanceDriveHoldFirst);
        // Same Fall keep of the Plant trunk lean. EnterFall resets
        // PhaseAge, DualLoadMinSpan never opens, CoM stays on the
        // stacked feet so leanRoom stays ~1, and Fall assigns
        // fallLean * fallLiftClock with PhaseAge 0: the 9° Plant lean
        // is a step to 0 on the first Fall frame — the third leg of
        // the initiation triangle. -walkPlantFallLeanHoldFirst 0
        // restores that 9°→0 drop. Catch / later cycles keep Fall/
        // Reach envelopes. Happy-path leanRoom→0 at the gate is a
        // no-op.
        balance.walkPlantFallLeanHoldFirst = GetFloatArg(
            "-walkPlantFallLeanHoldFirst", balance.walkPlantFallLeanHoldFirst);
        // Same Fall keep of the Plant pelvis-tilt unload. EnterFall
        // resets PhaseAge, DualLoadMinSpan never opens, liftGateBlend
        // stays 0, so tiltSoft × standLegLevel is 1.28° of the 10° that
        // raised the swing hip: DrivePelvis yanks toward vertical and
        // drops that hip (and fights the held 9° lean).
        // -walkPlantFallTiltHoldFirst 0 restores that 10°→1.28° drop
        // on the first Fall/Reach frame. Catch / later cycles keep
        // DualLoad tiltSoft. DrivePelvis stays; this is that loop's
        // target, not ThighHold.
        balance.walkPlantFallTiltHoldFirst = GetFloatArg(
            "-walkPlantFallTiltHoldFirst", balance.walkPlantFallTiltHoldFirst);
        // Same Fall keep of the Plant stance-knee straighten. EnterFall
        // resets PhaseAge, DualLoadMinSpan never opens, liftGateBlend
        // stays 0, so the fade schedule returns to its 0.5 end: extra 5°
        // of stance flex on the only strut. Fall vaultClock is 0 on
        // that same frame, so stanceVault does not skip the extra.
        // Those 5° drop the stance hip ~4 mm (2·L·cos(knee/2)), which
        // is swing-foot clearance. Hip-unload hold would be a no-op
        // (ThighHold already owns the swing hip on Fall via fold hold).
        // -walkPlantFallStanceStraightFirst 0 restores that extra 5° on
        // the first Fall/Reach frame. Catch / later cycles keep DualLoad
        // / vault. Not trailing A2 and not a Fall vault to 0°.
        balance.walkPlantFallStanceStraightFirst = GetFloatArg(
            "-walkPlantFallStanceStraightFirst", balance.walkPlantFallStanceStraightFirst);
        // Same Fall keep of the Plant rock-lead CoP. EnterFall replaces
        // comLead with fallComLead × blend (0.025 m) and the sole roll
        // starts at PhaseAge 0, so the first Fall frames pin CoM on a
        // stationary setpoint. Grounded Reach writes comLead 0: the
        // WALK-2 root (stance ankle holds CoM over the stacked feet)
        // while Fall→Reach still waits on air. Lean hold is the trunk
        // half of that roll-over; this is the ankle half. Hip-unload
        // hold would still be a no-op (ThighHold already owns the
        // swing hip on Fall via fold hold).
        // -walkPlantFallRockHoldFirst 0 restores 0.03→0.025 (Fall) /
        // 0.03→0 (Reach) on the first Fall/Reach frame. Catch / later
        // cycles keep fallComLead / RecoverBrake. Not trailing A2 and
        // not an ankle-angle PD.
        balance.walkPlantFallRockHoldFirst = GetFloatArg(
            "-walkPlantFallRockHoldFirst", balance.walkPlantFallRockHoldFirst);
        // Same Fall keep of Plant/Fall ankleScale 1. Fall already writes
        // 1, then after fallKneeRampSeconds the driver enters Reach with
        // the swing still on dirt. Reach Lerp(1, 0.25, calm) on a stacked
        // trunk is 0.25: the rock-lead hold's 0.03 m setpoint then makes
        // 25% of its torque, so CoM cannot finish the roll-over that
        // Fall→Reach is waiting on. Hold 1 while the swing sole is down
        // and comLead is live (scale 1 with comLead 0 is the WALK-2
        // root at full strength). Air Reach keeps 0.25. Hip-unload hold
        // would still be a no-op (ThighHold already owns the swing hip
        // on Fall via fold hold).
        // -walkPlantFallAnkleScaleHoldFirst 0 restores Reach 0.25 on
        // the first grounded Fall→Reach frame. Catch / later cycles
        // keep the Reach envelope. Not trailing A2 and not an
        // ankle-angle PD.
        balance.walkPlantFallAnkleScaleHoldFirst = GetFloatArg(
            "-walkPlantFallAnkleScaleHoldFirst", balance.walkPlantFallAnkleScaleHoldFirst);
        // Same Fall cap of the Plant swing fold. walkPlantFallFoldHold
        // Maxes Plant 55° across PhaseAge 0, then Fall's 2× clock
        // overtakes to reachKneeFlex 140° in fallKneeRampSeconds while
        // DualLoadMinSpan never opens. 140° on a still-shared plant is
        // the squat that drops the pelvis onto both soles (hip-to-ankle
        // 2·L·cos(knee/2): 55° ~0.78 L, 140° ~0.34 L). Cap at the Plant
        // pose while the swing sole is down. Air Fall/Reach keeps 140°.
        // Hip-unload hold would still be a no-op (ThighHold already
        // owns the swing hip on Fall via fold hold).
        // -walkPlantFallFoldCapFirst 0 restores the grounded ramp to
        // 140° on the first Fall/Reach. Catch / later cycles keep the
        // envelope. Not trailing A2 and not −swingHipFlex.
        balance.walkPlantFallFoldCapFirst = GetFloatArg(
            "-walkPlantFallFoldCapFirst", balance.walkPlantFallFoldCapFirst);
        // Same Fall hold of the Plant stance-hip loop scale. Plant writes
        // hipBalanceScale 1, then a calm stacked Fall (DualLoadMinSpan
        // never opens) Lerp(1, 0.15, calm) with calm = 1, Reach 0.25.
        // The 0.15 is a committed-topple cut; on dirt it sags the pelvis
        // into the 55° fold the cap just saved. Hold 1 while the swing
        // sole is down. Air Fall/Reach keeps 0.15 / 0.25. Hip-unload
        // hold would still be a no-op (ThighHold already owns the swing
        // hip on Fall via fold hold).
        // -walkPlantFallHipBalanceHoldFirst 0 restores Fall 0.15 /
        // Reach 0.25 on the first grounded Fall/Reach. Catch / later
        // cycles keep the envelope. Not trailing A2 and not a PD retune.
        balance.walkPlantFallHipBalanceHoldFirst = GetFloatArg(
            "-walkPlantFallHipBalanceHoldFirst", balance.walkPlantFallHipBalanceHoldFirst);
        // Same Fall keep of Plant suppressWalkLean. Plant zeros
        // walkAutoLean / walkBasePelvisLean (and the torso pair);
        // Fall/Reach never set the flag. Cycle 0 is a stacked trunk at
        // rest (DualLoadMinSpan never opens), so walkSpeedError = 1 and
        // the first grounded Fall frame adds 8° pelvis / 11.2° chest on
        // top of the held 9° fc lean. Lumbar ±20° takes that as a stop
        // slam; the third-law reaction cancels the roll-over Fall→Reach
        // is waiting on. Hold Plant's suppress while the swing sole is
        // down. Air Fall/Reach keeps speed/base lean. Hip-unload hold
        // would still be a no-op (ThighHold already owns the swing hip
        // on Fall via fold hold).
        // -walkPlantFallSuppressWalkLeanFirst 0 restores the 8°/11°
        // speed+base lean step on the first grounded Fall/Reach. Catch
        // / later cycles keep RecoverBrake / Plant suppress. Not
        // trailing A2, not a PD retune, and not a walkTargetSpeed
        // change.
        balance.walkPlantFallSuppressWalkLeanFirst = GetFloatArg(
            "-walkPlantFallSuppressWalkLeanFirst", balance.walkPlantFallSuppressWalkLeanFirst);
        // Same Fall raise of the grounded fold ceiling. Fold cap pinned
        // the shin at Plant 55° so 140° would not squat a sagging pelvis.
        // Hip-balance hold + suppressWalkLean now keep that strut (loop 1,
        // no 8°/11° lumbar slam), so 55° is the Plant floor. Quarter of
        // the remaining Fall fold (Lerp 55→140 at 0.25 = 76°) is still
        // ~0.79 L, not the 0.34 L squat. Air Fall/Reach keeps 140°.
        // Hip-unload hold would still be a no-op (ThighHold already owns
        // the swing hip on Fall via fold hold).
        // -walkPlantFallFoldGrowFirst 0 restores the former 55° pin on
        // the first grounded Fall/Reach. Catch / later cycles keep the
        // envelope. Not trailing A2, not −swingHipFlex, and not a PD
        // retune.
        balance.walkPlantFallFoldGrowFirst = GetFloatArg(
            "-walkPlantFallFoldGrowFirst", balance.walkPlantFallFoldGrowFirst);
        // Same Fall seed of the grow cap. Grow raised Min to 76°; fold
        // hold still Maxes Plant 55° at PhaseAge 0, and Fall's 2× clock
        // starts at 0, so the first ~8 Fall frames stay at the 55° that
        // already failed to clear. DualLoadMinSpan never opens: that is
        // the lift phase. Max to the grow cap on dirt so the extra
        // shortening (2·L·cos(38°) vs 2·L·cos(27.5°)) is commanded on
        // the first grounded Fall frame. Clock still Maxes if it would
        // exceed 76; Min still caps. Air Fall/Reach keeps 140°.
        // Hip-unload hold would still be a no-op (ThighHold already owns
        // the swing hip on Fall via fold hold).
        // -walkPlantFallFoldSeedFirst 0 restores the climb from Plant
        // 55° on the first grounded Fall/Reach. Catch / later cycles
        // keep the envelope. Not trailing A2, not −swingHipFlex, and
        // not a PD retune.
        balance.walkPlantFallFoldSeedFirst = GetFloatArg(
            "-walkPlantFallFoldSeedFirst", balance.walkPlantFallFoldSeedFirst);
        // Same Fall seed of the lift pair under that 76° fold. Fold
        // seed Maxes reachKnee immediately; hipLift/ankleLift stay at
        // the Plant leftover sized for 55° (0.5 cap × plantFold, fold
        // hold Maxes it, 2× lift clock starts at 0). Extra knee flex
        // without that flexor/unweight pair slides the foot back on
        // µ=0.4 — the backward start WALK-2 forbids. DualLoadMinSpan
        // never opens: that is the lift phase. Same quarter as fold
        // grow (Lerp Plant leftover → 1 at 0.25 = 0.625). Still a
        // Clamp01 bias, still fades at the 16–32° cap, not −swingHipFlex
        // and not a 1.0 dorsiflexor. Air Fall/Reach keeps Plant/Fall 0.5.
        // Hip-unload hold would still be a no-op (ThighHold already owns
        // the swing hip on Fall via fold hold).
        // -walkPlantFallLiftSeedFirst 0 restores the Plant 0.5
        // hipLift/ankleLift under the seeded 76° fold. Catch / later
        // cycles keep the envelope. Not trailing A2, not −swingHipFlex,
        // and not a PD retune.
        balance.walkPlantFallLiftSeedFirst = GetFloatArg(
            "-walkPlantFallLiftSeedFirst", balance.walkPlantFallLiftSeedFirst);
        // Same Fall seed of the cap floor under that 0.625 hipLift.
        // Lift seed raised the bias; ApplyFallCatchSwingThighHold still
        // floors the fade at 0.5, so at the 32° leftover (CapCeil) the
        // extra flexor is halved (liftSignal −0.3125 vs −0.391). DualLoadMinSpan
        // never opens: that is the lift phase. Same quarter as fold
        // grow / lift seed (Lerp 0.5 → 1 at 0.25 = 0.625). Still a
        // Clamp01 hold at the cap, not −swingHipFlex and not a 1.0
        // floor into 40°. Air Fall/Reach keeps the 0.5 floor.
        // Hip-unload hold would still be a no-op (ThighHold already owns
        // the swing hip on Fall via fold hold).
        // -walkPlantFallLiftFloorSeedFirst 0 restores the former 0.5
        // floor under the seeded 0.625 hipLift. Catch / later cycles
        // keep the envelope. Not trailing A2, not −swingHipFlex, and
        // not a PD retune.
        balance.walkPlantFallLiftFloorSeedFirst = GetFloatArg(
            "-walkPlantFallLiftFloorSeedFirst", balance.walkPlantFallLiftFloorSeedFirst);
        // Same Fall grow of the matched 0.625 trio. Floor seed matched
        // hipLift so leftover 32° (CapCeil) holds −0.391; remaining gap
        // to 1.0 is 0.375 of bias. DualLoadMinSpan never opens: that is
        // the lift phase. Same quarter as fold grow / lift seed / floor
        // seed (Lerp 0.625 → 1 at 0.25 = 0.719 on hipLift, ankleLift and
        // the floor together). liftSignal −0.517. Still a Clamp01 hold
        // at the cap, not −swingHipFlex and not a 1.0 floor into 40°.
        // Air Fall/Reach keeps 0.625. Hip-unload hold would still be a
        // no-op (ThighHold already owns the swing hip on Fall via fold
        // hold).
        // -walkPlantFallLiftGrowFirst 0 restores the former matched
        // 0.625 trio under the seeded 76° fold. Catch / later cycles
        // keep the envelope. Not trailing A2, not −swingHipFlex, and
        // not a PD retune.
        balance.walkPlantFallLiftGrowFirst = GetFloatArg(
            "-walkPlantFallLiftGrowFirst", balance.walkPlantFallLiftGrowFirst);
        // Same Fall second grow of the matched 0.719 trio. GrowFirst
        // matched leftover 32° (CapCeil) to −0.517; remaining gap to
        // 1.0 is 0.281 of bias. DualLoadMinSpan never opens: that is
        // the lift phase. Same quarter as fold grow / lift seed /
        // floor seed / GrowFirst (Lerp 0.719 → 1 at 0.25 = 0.789 on
        // hipLift, ankleLift and the floor together). liftSignal
        // −0.623. Still a Clamp01 hold at the cap, not −swingHipFlex
        // and not a 1.0 floor into 40°. Air Fall/Reach keeps 0.719.
        // Hip-unload hold would still be a no-op (ThighHold already
        // owns the swing hip on Fall via fold hold).
        // -walkPlantFallLiftGrowMoreFirst 0 restores the former matched
        // 0.719 trio under the seeded 76° fold. Catch / later cycles
        // keep the envelope. Not trailing A2, not −swingHipFlex, and
        // not a PD retune.
        balance.walkPlantFallLiftGrowMoreFirst = GetFloatArg(
            "-walkPlantFallLiftGrowMoreFirst", balance.walkPlantFallLiftGrowMoreFirst);
        // Same Fall third grow of the matched 0.789 trio. GrowMoreFirst
        // matched leftover 32° (CapCeil) to −0.623; remaining gap to
        // 1.0 is 0.211 of bias. DualLoadMinSpan never opens: that is
        // the lift phase. Same quarter as fold grow / lift seed /
        // floor seed / GrowFirst / GrowMore (Lerp 0.789 → 1 at 0.25
        // = 0.842 on hipLift, ankleLift and the floor together).
        // liftSignal −0.709. Still a Clamp01 hold at the cap, not
        // −swingHipFlex and not a 1.0 floor into 40°. Air Fall/Reach
        // keeps 0.789. Hip-unload hold would still be a no-op
        // (ThighHold already owns the swing hip on Fall via fold
        // hold).
        // -walkPlantFallLiftGrowYetFirst 0 restores the former matched
        // 0.789 trio under the seeded 76° fold. Catch / later cycles
        // keep the envelope. Not trailing A2, not −swingHipFlex, and
        // not a PD retune.
        balance.walkPlantFallLiftGrowYetFirst = GetFloatArg(
            "-walkPlantFallLiftGrowYetFirst", balance.walkPlantFallLiftGrowYetFirst);
        // Same Fall fourth grow of the matched 0.842 trio. GrowYetFirst
        // matched leftover 32° (CapCeil) to −0.709; remaining gap to
        // 1.0 is 0.158 of bias. DualLoadMinSpan never opens: that is
        // the lift phase. Same quarter as fold grow / lift seed /
        // floor seed / GrowFirst / GrowMore / GrowYet (Lerp 0.842
        // → 1 at 0.25 = 0.882 on hipLift, ankleLift and the floor
        // together). liftSignal −0.778. Still a Clamp01 hold at the
        // cap, not −swingHipFlex and not a 1.0 floor into 40°. Air
        // Fall/Reach keeps 0.842. Hip-unload hold would still be a
        // no-op (ThighHold already owns the swing hip on Fall via
        // fold hold).
        // -walkPlantFallLiftGrowEvenFirst 0 restores the former matched
        // 0.842 trio under the seeded 76° fold. Catch / later cycles
        // keep the envelope. Not trailing A2, not −swingHipFlex, and
        // not a PD retune.
        balance.walkPlantFallLiftGrowEvenFirst = GetFloatArg(
            "-walkPlantFallLiftGrowEvenFirst", balance.walkPlantFallLiftGrowEvenFirst);
        // Same Fall fifth grow of the matched 0.882 trio. GrowEvenFirst
        // matched leftover 32° (CapCeil) to −0.778; remaining gap to
        // 1.0 is 0.118 of bias. DualLoadMinSpan never opens: that is
        // the lift phase. Same quarter as fold grow / lift seed /
        // floor seed / GrowFirst / GrowMore / GrowYet / GrowEven
        // (Lerp 0.882 → 1 at 0.25 = 0.912 on hipLift, ankleLift and
        // the floor together). liftSignal −0.831. Still a Clamp01
        // hold at the cap, not −swingHipFlex and not a 1.0 floor
        // into 40°. Air Fall/Reach keeps 0.882. Hip-unload hold
        // would still be a no-op (ThighHold already owns the swing
        // hip on Fall via fold hold).
        // -walkPlantFallLiftGrowStillFirst 0 restores the former
        // matched 0.882 trio under the seeded 76° fold. Catch /
        // later cycles keep the envelope. Not trailing A2, not
        // −swingHipFlex, and not a PD retune.
        balance.walkPlantFallLiftGrowStillFirst = GetFloatArg(
            "-walkPlantFallLiftGrowStillFirst", balance.walkPlantFallLiftGrowStillFirst);
        // Same Fall sixth grow of the matched 0.912 trio. GrowStillFirst
        // matched leftover 32° (CapCeil) to −0.831; remaining gap to
        // 1.0 is 0.088 of bias. DualLoadMinSpan never opens: that is
        // the lift phase. Same quarter as fold grow / lift seed /
        // floor seed / GrowFirst / GrowMore / GrowYet / GrowEven /
        // GrowStill (Lerp 0.912 → 1 at 0.25 = 0.934 on hipLift,
        // ankleLift and the floor together). liftSignal −0.872. Still
        // a Clamp01 hold at the cap, not −swingHipFlex and not a 1.0
        // floor into 40°. Air Fall/Reach keeps 0.912. Hip-unload hold
        // would still be a no-op (ThighHold already owns the swing
        // hip on Fall via fold hold).
        // -walkPlantFallLiftGrowAgainFirst 0 restores the former
        // matched 0.912 trio under the seeded 76° fold. Catch /
        // later cycles keep the envelope. Not trailing A2, not
        // −swingHipFlex, and not a PD retune.
        balance.walkPlantFallLiftGrowAgainFirst = GetFloatArg(
            "-walkPlantFallLiftGrowAgainFirst", balance.walkPlantFallLiftGrowAgainFirst);
        // Same Fall seventh grow of the matched 0.934 trio. GrowAgainFirst
        // matched leftover 32° (CapCeil) to −0.872; remaining gap to
        // 1.0 is 0.066 of bias. DualLoadMinSpan never opens: that is
        // the lift phase. Same quarter as fold grow / lift seed /
        // floor seed / GrowFirst / GrowMore / GrowYet / GrowEven /
        // GrowStill / GrowAgain (Lerp 0.934 → 1 at 0.25 = 0.951 on
        // hipLift, ankleLift and the floor together). liftSignal
        // −0.904. Still a Clamp01 hold at the cap, not −swingHipFlex
        // and not a 1.0 floor into 40°. Air Fall/Reach keeps 0.934.
        // Hip-unload hold would still be a no-op (ThighHold already
        // owns the swing hip on Fall via fold hold).
        // -walkPlantFallLiftGrowAlsoFirst 0 restores the former
        // matched 0.934 trio under the seeded 76° fold. Catch /
        // later cycles keep the envelope. Not trailing A2, not
        // −swingHipFlex, and not a PD retune.
        balance.walkPlantFallLiftGrowAlsoFirst = GetFloatArg(
            "-walkPlantFallLiftGrowAlsoFirst", balance.walkPlantFallLiftGrowAlsoFirst);
        // Same Fall eighth grow of the matched 0.951 trio. GrowAlsoFirst
        // matched leftover 32° (CapCeil) to −0.904; remaining gap to
        // 1.0 is 0.049 of bias. DualLoadMinSpan never opens: that is
        // the lift phase. Same quarter as fold grow / lift seed /
        // floor seed / GrowFirst / GrowMore / GrowYet / GrowEven /
        // GrowStill / GrowAgain / GrowAlso (Lerp 0.951 → 1 at 0.25
        // = 0.963 on hipLift, ankleLift and the floor together).
        // liftSignal −0.927. Still a Clamp01 hold at the cap, not
        // −swingHipFlex and not a 1.0 floor into 40°. Air Fall/Reach
        // keeps 0.951. Hip-unload hold would still be a no-op
        // (ThighHold already owns the swing hip on Fall via fold
        // hold).
        // -walkPlantFallLiftGrowExtraFirst 0 restores the former
        // matched 0.951 trio under the seeded 76° fold. Catch /
        // later cycles keep the envelope. Not trailing A2, not
        // −swingHipFlex, and not a PD retune.
        balance.walkPlantFallLiftGrowExtraFirst = GetFloatArg(
            "-walkPlantFallLiftGrowExtraFirst", balance.walkPlantFallLiftGrowExtraFirst);
        // Same Fall ninth grow of the matched 0.963 trio. GrowExtraFirst
        // matched leftover 32° (CapCeil) to −0.927; remaining gap to
        // 1.0 is 0.037 of bias. DualLoadMinSpan never opens: that is
        // the lift phase. Same quarter as fold grow / lift seed /
        // floor seed / GrowFirst / GrowMore / GrowYet / GrowEven /
        // GrowStill / GrowAgain / GrowAlso / GrowExtra (Lerp 0.963 → 1
        // at 0.25 = 0.972 on hipLift, ankleLift and the floor together).
        // liftSignal −0.945. Still a Clamp01 hold at the cap, not
        // −swingHipFlex and not a 1.0 floor into 40°. Air Fall/Reach
        // keeps 0.963. Hip-unload hold would still be a no-op
        // (ThighHold already owns the swing hip on Fall via fold
        // hold).
        // -walkPlantFallLiftGrowPlusFirst 0 restores the former
        // matched 0.963 trio under the seeded 76° fold. Catch /
        // later cycles keep the envelope. Not trailing A2, not
        // −swingHipFlex, and not a PD retune.
        balance.walkPlantFallLiftGrowPlusFirst = GetFloatArg(
            "-walkPlantFallLiftGrowPlusFirst", balance.walkPlantFallLiftGrowPlusFirst);
        // Same Fall quarter of the 32° CapCeil leftover toward sci_a 40°.
        // PlusFirst matched leftover 32° (room ≤ 0) to −0.945; extra
        // hipLift at room ≤ 0 does not raise past CapCeil, and the
        // couple Min clips a pre-clamp 34° write back to 32°.
        // DualLoadMinSpan never opens: that is the lift phase. Same
        // quarter as fold grow (Lerp 32 → 40 at 0.25 = 34° after the
        // couple clamp). At a 32° thigh, room = 2°. liftSignal stays
        // −0.945 until the thigh walks. Still 6° short of sci_a, not
        // −swingHipFlex. Air Fall/Reach keeps 32°. Hip-unload hold
        // would still be a no-op (ThighHold already owns the swing hip
        // on Fall via fold hold).
        // -walkPlantFallLiftCapGrowFirst 0 restores the former 32°
        // CapCeil leftover under the 0.972 trio. Catch / later cycles
        // keep the envelope. Not trailing A2, not −swingHipFlex, and
        // not a PD retune.
        balance.walkPlantFallLiftCapGrowFirst = GetFloatArg(
            "-walkPlantFallLiftCapGrowFirst", balance.walkPlantFallLiftCapGrowFirst);
        // Same Fall second quarter of the 34° CapGrowFirst leftover
        // toward sci_a 40°. CapGrowFirst wrote leftover 34° (room 2°
        // at a 32° thigh); extra hipLift at room ≤ 0 on 34° does not
        // raise past that cap. DualLoadMinSpan never opens: that is
        // the lift phase. Same quarter as fold grow (Lerp 34 → 40 at
        // 0.25 = 35.5°). At a 34° thigh, room = 1.5°. liftSignal stays
        // −0.945 until the thigh walks. Still 4.5° short of sci_a, not
        // −swingHipFlex. Air Fall/Reach keeps 34°. Hip-unload hold
        // would still be a no-op (ThighHold already owns the swing hip
        // on Fall via fold hold).
        // -walkPlantFallLiftCapGrowMoreFirst 0 restores the former 34°
        // CapGrowFirst leftover under the 0.972 trio. Catch / later
        // cycles keep the envelope. Not trailing A2, not −swingHipFlex,
        // and not a PD retune.
        balance.walkPlantFallLiftCapGrowMoreFirst = GetFloatArg(
            "-walkPlantFallLiftCapGrowMoreFirst", balance.walkPlantFallLiftCapGrowMoreFirst);
        // Same Fall third quarter of the 35.5° CapGrowMoreFirst leftover
        // toward sci_a 40°. CapGrowMoreFirst wrote leftover 35.5° (room
        // 1.5° at a 34° thigh); extra hipLift at room ≤ 0 on 35.5° does
        // not raise past that cap. DualLoadMinSpan never opens: that is
        // the lift phase. Same quarter as fold grow (Lerp 35.5 → 40 at
        // 0.25 = 36.625°). At a 35.5° thigh, room = 1.125°. liftSignal
        // stays −0.945 until the thigh walks. Still 3.375° short of
        // sci_a, not −swingHipFlex. Air Fall/Reach keeps 35.5°.
        // Hip-unload hold would still be a no-op (ThighHold already
        // owns the swing hip on Fall via fold hold).
        // -walkPlantFallLiftCapGrowYetFirst 0 restores the former 35.5°
        // CapGrowMoreFirst leftover under the 0.972 trio. Catch /
        // later cycles keep the envelope. Not trailing A2, not
        // −swingHipFlex, and not a PD retune.
        balance.walkPlantFallLiftCapGrowYetFirst = GetFloatArg(
            "-walkPlantFallLiftCapGrowYetFirst", balance.walkPlantFallLiftCapGrowYetFirst);
        // Same Fall fourth quarter of the 36.625° CapGrowYetFirst leftover
        // toward sci_a 40°. CapGrowYetFirst wrote leftover 36.625° (room
        // 1.125° at a 35.5° thigh); extra hipLift at room ≤ 0 on 36.625°
        // does not raise past that cap. DualLoadMinSpan never opens:
        // that is the lift phase. Same quarter as fold grow (Lerp
        // 36.625 → 40 at 0.25 = 37.46875°). At a 36.625° thigh, room =
        // 0.84375°. liftSignal stays −0.945 until the thigh walks.
        // Still 2.53125° short of sci_a, not −swingHipFlex. Air Fall/
        // Reach keeps 36.625°. Hip-unload hold would still be a no-op
        // (ThighHold already owns the swing hip on Fall via fold hold).
        // -walkPlantFallLiftCapGrowEvenFirst 0 restores the former
        // 36.625° CapGrowYetFirst leftover under the 0.972 trio. Catch
        // / later cycles keep the envelope. Not trailing A2, not
        // −swingHipFlex, and not a PD retune.
        balance.walkPlantFallLiftCapGrowEvenFirst = GetFloatArg(
            "-walkPlantFallLiftCapGrowEvenFirst", balance.walkPlantFallLiftCapGrowEvenFirst);
        // Same Fall fifth quarter of the 37.46875° CapGrowEvenFirst leftover
        // toward sci_a 40°. CapGrowEvenFirst wrote leftover 37.46875°
        // (room 0.84375° at a 36.625° thigh); extra hipLift at room ≤ 0
        // on 37.46875° does not raise past that cap. DualLoadMinSpan
        // never opens: that is the lift phase. Same quarter as fold
        // grow (Lerp 37.46875 → 40 at 0.25 = 38.1015625°). At a
        // 37.46875° thigh, room = 0.6328125°. liftSignal stays −0.945
        // until the thigh walks. Still 1.8984375° short of sci_a, not
        // −swingHipFlex. Air Fall/Reach keeps 37.46875°. Hip-unload
        // hold would still be a no-op (ThighHold already owns the swing
        // hip on Fall via fold hold).
        // -walkPlantFallLiftCapGrowStillFirst 0 restores the former
        // 37.46875° CapGrowEvenFirst leftover under the 0.972 trio.
        // Catch / later cycles keep the envelope. Not trailing A2, not
        // −swingHipFlex, and not a PD retune.
        balance.walkPlantFallLiftCapGrowStillFirst = GetFloatArg(
            "-walkPlantFallLiftCapGrowStillFirst", balance.walkPlantFallLiftCapGrowStillFirst);
        // Same Fall sixth quarter of the 38.1015625° CapGrowStillFirst leftover
        // toward sci_a 40°. CapGrowStillFirst wrote leftover 38.1015625°
        // (room 0.6328125° at a 37.46875° thigh); extra hipLift at room ≤ 0
        // on 38.1015625° does not raise past that cap. DualLoadMinSpan
        // never opens: that is the lift phase. Same quarter as fold
        // grow (Lerp 38.1015625 → 40 at 0.25 = 38.576171875°). At a
        // 38.1015625° thigh, room = 0.474609375°. liftSignal stays −0.945
        // until the thigh walks. Still 1.423828125° short of sci_a, not
        // −swingHipFlex. Air Fall/Reach keeps 38.1015625°. Hip-unload
        // hold would still be a no-op (ThighHold already owns the swing
        // hip on Fall via fold hold).
        // -walkPlantFallLiftCapGrowAgainFirst 0 restores the former
        // 38.1015625° CapGrowStillFirst leftover under the 0.972 trio.
        // Catch / later cycles keep the envelope. Not trailing A2, not
        // −swingHipFlex, and not a PD retune.
        balance.walkPlantFallLiftCapGrowAgainFirst = GetFloatArg(
            "-walkPlantFallLiftCapGrowAgainFirst", balance.walkPlantFallLiftCapGrowAgainFirst);
        // Same Fall seventh quarter of the 38.576171875° CapGrowAgainFirst leftover
        // toward sci_a 40°. CapGrowAgainFirst wrote leftover 38.576171875°
        // (room 0.474609375° at a 38.1015625° thigh); extra hipLift at room ≤ 0
        // on 38.576171875° does not raise past that cap. DualLoadMinSpan
        // never opens: that is the lift phase. Same quarter as fold
        // grow (Lerp 38.576171875 → 40 at 0.25 = 38.93212890625°). At a
        // 38.576171875° thigh, room = 0.35595703125°. liftSignal stays −0.945
        // until the thigh walks. Still 1.06787109375° short of sci_a, not
        // −swingHipFlex. Air Fall/Reach keeps 38.576171875°. Hip-unload
        // hold would still be a no-op (ThighHold already owns the swing hip
        // on Fall via fold hold).
        // -walkPlantFallLiftCapGrowAlsoFirst 0 restores the former
        // 38.576171875° CapGrowAgainFirst leftover under the 0.972 trio.
        // Catch / later cycles keep the envelope. Not trailing A2, not
        // −swingHipFlex, and not a PD retune.
        balance.walkPlantFallLiftCapGrowAlsoFirst = GetFloatArg(
            "-walkPlantFallLiftCapGrowAlsoFirst", balance.walkPlantFallLiftCapGrowAlsoFirst);
        // Same Fall eighth quarter of the 38.93212890625° CapGrowAlsoFirst leftover
        // toward sci_a 40°. CapGrowAlsoFirst wrote leftover 38.93212890625°
        // (room 0.35595703125° at a 38.576171875° thigh); extra hipLift at room ≤ 0
        // on 38.93212890625° does not raise past that cap. DualLoadMinSpan
        // never opens: that is the lift phase. Same quarter as fold
        // grow (Lerp 38.93212890625 → 40 at 0.25 = 39.1990966796875°). At a
        // 38.93212890625° thigh, room = 0.2669677734375°. liftSignal stays −0.945
        // until the thigh walks. Still 0.8009033203125° short of sci_a, not
        // −swingHipFlex. Air Fall/Reach keeps 38.93212890625°. Hip-unload
        // hold would still be a no-op (ThighHold already owns the swing hip
        // on Fall via fold hold).
        // -walkPlantFallLiftCapGrowExtraFirst 0 restores the former
        // 38.93212890625° CapGrowAlsoFirst leftover under the 0.972 trio.
        // Catch / later cycles keep the envelope. Not trailing A2, not
        // −swingHipFlex, and not a PD retune.
        balance.walkPlantFallLiftCapGrowExtraFirst = GetFloatArg(
            "-walkPlantFallLiftCapGrowExtraFirst", balance.walkPlantFallLiftCapGrowExtraFirst);
        // Same Fall ninth quarter of the 39.1990966796875° CapGrowExtraFirst leftover
        // toward sci_a 40°. CapGrowExtraFirst wrote leftover 39.1990966796875°
        // (room 0.2669677734375° at a 38.93212890625° thigh); extra hipLift at room ≤ 0
        // on 39.1990966796875° does not raise past that cap. DualLoadMinSpan
        // never opens: that is the lift phase. Same quarter as fold
        // grow (Lerp 39.1990966796875 → 40 at 0.25 = 39.399322509765625°). At a
        // 39.1990966796875° thigh, room = 0.200225830078125°. liftSignal stays −0.945
        // until the thigh walks. Still 0.600677490234375° short of sci_a, not
        // −swingHipFlex. Air Fall/Reach keeps 39.1990966796875°. Hip-unload
        // hold would still be a no-op (ThighHold already owns the swing hip
        // on Fall via fold hold).
        // -walkPlantFallLiftCapGrowPlusFirst 0 restores the former
        // 39.1990966796875° CapGrowExtraFirst leftover under the 0.972 trio.
        // Catch / later cycles keep the envelope. Not trailing A2, not
        // −swingHipFlex, and not a PD retune.
        balance.walkPlantFallLiftCapGrowPlusFirst = GetFloatArg(
            "-walkPlantFallLiftCapGrowPlusFirst", balance.walkPlantFallLiftCapGrowPlusFirst);
        // Same Fall tenth quarter of the 39.399322509765625° CapGrowPlusFirst leftover
        // toward sci_a 40°. CapGrowPlusFirst wrote leftover 39.399322509765625°
        // (room 0.200225830078125° at a 39.1990966796875° thigh); extra hipLift at room ≤ 0
        // on 39.399322509765625° does not raise past that cap. DualLoadMinSpan
        // never opens: that is the lift phase. Same quarter as fold
        // grow (Lerp 39.399322509765625 → 40 at 0.25 = 39.54949188232421875°). At a
        // 39.399322509765625° thigh, room = 0.15016937255859375°. liftSignal stays −0.945
        // until the thigh walks. Still 0.45050811767578125° short of sci_a, not
        // −swingHipFlex. Air Fall/Reach keeps 39.399322509765625°. Hip-unload
        // hold would still be a no-op (ThighHold already owns the swing hip
        // on Fall via fold hold).
        // -walkPlantFallLiftCapGrowFurtherFirst 0 restores the former
        // 39.399322509765625° CapGrowPlusFirst leftover under the 0.972 trio.
        // Catch / later cycles keep the envelope. Not trailing A2, not
        // −swingHipFlex, and not a PD retune.
        balance.walkPlantFallLiftCapGrowFurtherFirst = GetFloatArg(
            "-walkPlantFallLiftCapGrowFurtherFirst", balance.walkPlantFallLiftCapGrowFurtherFirst);
        // Same Fall eleventh quarter of the 39.54949188232421875° CapGrowFurtherFirst leftover
        // toward sci_a 40°. CapGrowFurtherFirst wrote leftover 39.54949188232421875°
        // (room 0.15016937255859375° at a 39.399322509765625° thigh); extra hipLift at room ≤ 0
        // on 39.54949188232421875° does not raise past that cap. DualLoadMinSpan
        // never opens: that is the lift phase. Same quarter as fold
        // grow (Lerp 39.54949188232421875 → 40 at 0.25 = 39.6621189117431640625°). At a
        // 39.54949188232421875° thigh, room = 0.1126270294189453125°. liftSignal stays −0.945
        // until the thigh walks. Still 0.3378810882568359375° short of sci_a, not
        // −swingHipFlex. Air Fall/Reach keeps 39.54949188232421875°. Hip-unload
        // hold would still be a no-op (ThighHold already owns the swing hip
        // on Fall via fold hold).
        // -walkPlantFallLiftCapGrowOnwardFirst 0 restores the former
        // 39.54949188232421875° CapGrowFurtherFirst leftover under the 0.972 trio.
        // Catch / later cycles keep the envelope. Not trailing A2, not
        // −swingHipFlex, and not a PD retune.
        balance.walkPlantFallLiftCapGrowOnwardFirst = GetFloatArg(
            "-walkPlantFallLiftCapGrowOnwardFirst", balance.walkPlantFallLiftCapGrowOnwardFirst);
        // Same Fall twelfth quarter of the 39.6621189117431640625° CapGrowOnwardFirst leftover
        // toward sci_a 40°. CapGrowOnwardFirst wrote leftover 39.6621189117431640625°
        // (room 0.1126270294189453125° at a 39.54949188232421875° thigh); extra hipLift at room ≤ 0
        // on 39.6621189117431640625° does not raise past that cap. DualLoadMinSpan
        // never opens: that is the lift phase. Same quarter as fold
        // grow (Lerp 39.6621189117431640625 → 40 at 0.25 = 39.746589183807373046875°). At a
        // 39.6621189117431640625° thigh, room = 0.084470272064208984375°. liftSignal stays −0.945
        // until the thigh walks. Still 0.253410816192626953125° short of sci_a, not
        // −swingHipFlex. Air Fall/Reach keeps 39.6621189117431640625°. Hip-unload
        // hold would still be a no-op (ThighHold already owns the swing hip
        // on Fall via fold hold).
        // -walkPlantFallLiftCapGrowAheadFirst 0 restores the former
        // 39.6621189117431640625° CapGrowOnwardFirst leftover under the 0.972 trio.
        // Catch / later cycles keep the envelope. Not trailing A2, not
        // −swingHipFlex, and not a PD retune.
        balance.walkPlantFallLiftCapGrowAheadFirst = GetFloatArg(
            "-walkPlantFallLiftCapGrowAheadFirst", balance.walkPlantFallLiftCapGrowAheadFirst);
        // Same Fall thirteenth quarter of the 39.746589183807373046875° CapGrowAheadFirst leftover
        // toward sci_a 40°. CapGrowAheadFirst wrote leftover 39.746589183807373046875°
        // (room 0.084470272064208984375° at a 39.6621189117431640625° thigh); extra hipLift at room ≤ 0
        // on 39.746589183807373046875° does not raise past that cap. DualLoadMinSpan
        // never opens: that is the lift phase. Same quarter as fold
        // grow (Lerp 39.746589183807373046875 → 40 at 0.25 = 39.80994188785552978515625°). At a
        // 39.746589183807373046875° thigh, room = 0.06335270404815673828125°. liftSignal stays −0.945
        // until the thigh walks. Still 0.19005811214447021484375° short of sci_a, not
        // −swingHipFlex. Air Fall/Reach keeps 39.746589183807373046875°. Hip-unload
        // hold would still be a no-op (ThighHold already owns the swing hip
        // on Fall via fold hold).
        // -walkPlantFallLiftCapGrowAlongFirst 0 restores the former
        // 39.746589183807373046875° CapGrowAheadFirst leftover under the 0.972 trio.
        // Catch / later cycles keep the envelope. Not trailing A2, not
        // −swingHipFlex, and not a PD retune.
        balance.walkPlantFallLiftCapGrowAlongFirst = GetFloatArg(
            "-walkPlantFallLiftCapGrowAlongFirst", balance.walkPlantFallLiftCapGrowAlongFirst);
        // Same Fall fourteenth quarter of the 39.80994188785552978515625° CapGrowAlongFirst leftover
        // toward sci_a 40°. CapGrowAlongFirst wrote leftover 39.80994188785552978515625°
        // (room 0.06335270404815673828125° at a 39.746589183807373046875° thigh); extra hipLift at room ≤ 0
        // on 39.80994188785552978515625° does not raise past that cap. DualLoadMinSpan
        // never opens: that is the lift phase. Same quarter as fold
        // grow (Lerp 39.80994188785552978515625 → 40 at 0.25 = 39.8574564158916473388671875°). At a
        // 39.80994188785552978515625° thigh, room = 0.0475145280361175537109375°. liftSignal stays −0.945
        // until the thigh walks. Still 0.1425435841083526611328125° short of sci_a, not
        // −swingHipFlex. Air Fall/Reach keeps 39.80994188785552978515625°. Hip-unload
        // hold would still be a no-op (ThighHold already owns the swing hip
        // on Fall via fold hold).
        // -walkPlantFallLiftCapGrowForthFirst 0 restores the former
        // 39.80994188785552978515625° CapGrowAlongFirst leftover under the 0.972 trio.
        // Catch / later cycles keep the envelope. Not trailing A2, not
        // −swingHipFlex, and not a PD retune.
        balance.walkPlantFallLiftCapGrowForthFirst = GetFloatArg(
            "-walkPlantFallLiftCapGrowForthFirst", balance.walkPlantFallLiftCapGrowForthFirst);
        // Same Fall fifteenth quarter of the 39.8574564158916473388671875° CapGrowForthFirst leftover
        // toward sci_a 40°. CapGrowForthFirst wrote leftover 39.8574564158916473388671875°
        // (room 0.0475145280361175537109375° at a 39.80994188785552978515625° thigh); extra hipLift at room ≤ 0
        // on 39.8574564158916473388671875° does not raise past that cap. DualLoadMinSpan
        // never opens: that is the lift phase. Same quarter as fold
        // grow (Lerp 39.8574564158916473388671875 → 40 at 0.25 = 39.893092311918735504150390625°). At a
        // 39.8574564158916473388671875° thigh, room = 0.035635896027088165283203125°. liftSignal stays −0.945
        // until the thigh walks. Still 0.106907688081264495849609375° short of sci_a, not
        // −swingHipFlex. Air Fall/Reach keeps 39.8574564158916473388671875°. Hip-unload
        // hold would still be a no-op (ThighHold already owns the swing hip
        // on Fall via fold hold).
        // -walkPlantFallLiftCapGrowBeyondFirst 0 restores the former
        // 39.8574564158916473388671875° CapGrowForthFirst leftover under the 0.972 trio.
        // Catch / later cycles keep the envelope. Not trailing A2, not
        // −swingHipFlex, and not a PD retune.
        balance.walkPlantFallLiftCapGrowBeyondFirst = GetFloatArg(
            "-walkPlantFallLiftCapGrowBeyondFirst", balance.walkPlantFallLiftCapGrowBeyondFirst);
        // Same Fall sixteenth quarter of the 39.893092311918735504150390625° CapGrowBeyondFirst leftover
        // toward sci_a 40°. CapGrowBeyondFirst wrote leftover 39.893092311918735504150390625°
        // (room 0.035635896027088165283203125° at a 39.8574564158916473388671875° thigh); extra hipLift at room ≤ 0
        // on 39.893092311918735504150390625° does not raise past that cap. DualLoadMinSpan
        // never opens: that is the lift phase. Same quarter as fold
        // grow (Lerp 39.893092311918735504150390625 → 40 at 0.25 = 39.91981923393905162811279296875°). At a
        // 39.893092311918735504150390625° thigh, room = 0.02672692202031612396240234375°. liftSignal stays −0.945
        // until the thigh walks. Still 0.08018076606094837188720703125° short of sci_a, not
        // −swingHipFlex. Air Fall/Reach keeps 39.893092311918735504150390625°. Hip-unload
        // hold would still be a no-op (ThighHold already owns the swing hip
        // on Fall via fold hold).
        // -walkPlantFallLiftCapGrowPastFirst 0 restores the former
        // 39.893092311918735504150390625° CapGrowBeyondFirst leftover under the 0.972 trio.
        // Catch / later cycles keep the envelope. Not trailing A2, not
        // −swingHipFlex, and not a PD retune.
        balance.walkPlantFallLiftCapGrowPastFirst = GetFloatArg(
            "-walkPlantFallLiftCapGrowPastFirst", balance.walkPlantFallLiftCapGrowPastFirst);
        // Same Fall seventeenth quarter of the 39.91981923393905162811279296875° CapGrowPastFirst leftover
        // toward sci_a 40°. CapGrowPastFirst wrote leftover 39.91981923393905162811279296875°
        // (room 0.02672692202031612396240234375° at a 39.893092311918735504150390625° thigh); extra hipLift at room ≤ 0
        // on 39.91981923393905162811279296875° does not raise past that cap. DualLoadMinSpan
        // never opens: that is the lift phase. Same quarter as fold
        // grow (Lerp 39.91981923393905162811279296875 → 40 at 0.25 = 39.9398644254542887210845947265625°). At a
        // 39.91981923393905162811279296875° thigh, room = 0.0200451915152370929718017578125°. liftSignal stays −0.945
        // until the thigh walks. Still 0.0601355745457112789154052734375° short of sci_a, not
        // −swingHipFlex. Air Fall/Reach keeps 39.91981923393905162811279296875°. Hip-unload
        // hold would still be a no-op (ThighHold already owns the swing hip
        // on Fall via fold hold).
        // -walkPlantFallLiftCapGrowOverFirst 0 restores the former
        // 39.91981923393905162811279296875° CapGrowPastFirst leftover under the 0.972 trio.
        // Catch / later cycles keep the envelope. Not trailing A2, not
        // −swingHipFlex, and not a PD retune.
        balance.walkPlantFallLiftCapGrowOverFirst = GetFloatArg(
            "-walkPlantFallLiftCapGrowOverFirst", balance.walkPlantFallLiftCapGrowOverFirst);
        // Same Fall eighteenth quarter of the 39.9398644254542887210845947265625° CapGrowOverFirst leftover
        // toward sci_a 40°. CapGrowOverFirst wrote leftover 39.9398644254542887210845947265625°
        // (room 0.0200451915152370929718017578125° at a 39.91981923393905162811279296875° thigh); extra hipLift at room ≤ 0
        // on 39.9398644254542887210845947265625° does not raise past that cap. DualLoadMinSpan
        // never opens: that is the lift phase. Same quarter as fold
        // grow (Lerp 39.9398644254542887210845947265625 → 40 at 0.25 = 39.954898319090716540813446044921875°). At a
        // 39.9398644254542887210845947265625° thigh, room = 0.015033893636427819728851318359375°. liftSignal stays −0.945
        // until the thigh walks. Still 0.045101680909283459186553955078125° short of sci_a, not
        // −swingHipFlex. Air Fall/Reach keeps 39.9398644254542887210845947265625°. Hip-unload
        // hold would still be a no-op (ThighHold already owns the swing hip
        // on Fall via fold hold).
        // -walkPlantFallLiftCapGrowAcrossFirst 0 restores the former
        // 39.9398644254542887210845947265625° CapGrowOverFirst leftover under the 0.972 trio.
        // Catch / later cycles keep the envelope. Not trailing A2, not
        // −swingHipFlex, and not a PD retune.
        balance.walkPlantFallLiftCapGrowAcrossFirst = GetFloatArg(
            "-walkPlantFallLiftCapGrowAcrossFirst", balance.walkPlantFallLiftCapGrowAcrossFirst);
        // Same Fall second quarter of the grounded fold. FoldGrowFirst
        // wrote 76° (Lerp 55→140 at 0.25). Leftover-cap grow through
        // AcrossFirst is spent: 0.045° short of sci_a, ~0.011 cm of a
        // 0.43 m thigh, fade stuck at 0.972 so liftSignal stays −0.945.
        // DualLoadMinSpan never opens: that is the lift phase. Knee
        // torque is thigh–shin, so more fold shortens the swing chain
        // without asking the hip past 40°. Same quarter as fold grow
        // (Lerp 76 → 140 at 0.25 = 92°, 2·L·cos(46°) ≈ 0.70 L vs
        // 2·L·cos(38°) ≈ 0.79 L, ~8 cm of a 0.86 m chain). Seed already
        // Maxes to groundedCap. Air Fall/Reach keeps 140°. Hip-unload
        // hold would still be a no-op (ThighHold already owns the swing
        // hip on Fall via fold hold).
        // -walkPlantFallFoldGrowMoreFirst 0 restores the former 76°
        // grow cap on the first grounded Fall/Reach. Catch / later
        // cycles keep the envelope. Not trailing A2, not −swingHipFlex,
        // and not a PD retune.
        balance.walkPlantFallFoldGrowMoreFirst = GetFloatArg(
            "-walkPlantFallFoldGrowMoreFirst", balance.walkPlantFallFoldGrowMoreFirst);
        // Same Fall third quarter of the grounded fold. FoldGrowMoreFirst
        // wrote 92° (Lerp 76→140 at 0.25). Leftover-cap grow through
        // AcrossFirst is spent: 0.045° short of sci_a, ~0.011 cm of a
        // 0.43 m thigh, fade stuck at 0.972 so liftSignal stays −0.945.
        // DualLoadMinSpan never opens: that is the lift phase. Knee
        // torque is thigh–shin, so more fold shortens the swing chain
        // without asking the hip past 40°. Same quarter as fold grow
        // (Lerp 92 → 140 at 0.25 = 104°, 2·L·cos(52°) ≈ 0.62 L vs
        // 2·L·cos(46°) ≈ 0.70 L, ~7 cm of a 0.86 m chain). Seed already
        // Maxes to groundedCap. Air Fall/Reach keeps 140°. Hip-unload
        // hold would still be a no-op (ThighHold already owns the swing
        // hip on Fall via fold hold).
        // -walkPlantFallFoldGrowYetFirst 0 restores the former 92°
        // grow cap on the first grounded Fall/Reach. Catch / later
        // cycles keep the envelope. Not trailing A2, not −swingHipFlex,
        // and not a PD retune.
        balance.walkPlantFallFoldGrowYetFirst = GetFloatArg(
            "-walkPlantFallFoldGrowYetFirst", balance.walkPlantFallFoldGrowYetFirst);
        // Same Fall fourth quarter of the grounded fold. FoldGrowYetFirst
        // wrote 104° (Lerp 92→140 at 0.25). Leftover-cap grow through
        // AcrossFirst is spent: 0.045° short of sci_a, ~0.011 cm of a
        // 0.43 m thigh, fade stuck at 0.972 so liftSignal stays −0.945.
        // DualLoadMinSpan never opens: that is the lift phase. Knee
        // torque is thigh–shin, so more fold shortens the swing chain
        // without asking the hip past 40°. Same quarter as fold grow
        // (Lerp 104 → 140 at 0.25 = 113°, 2·L·cos(56.5°) ≈ 0.55 L vs
        // 2·L·cos(52°) ≈ 0.62 L, ~6 cm of a 0.86 m chain). Seed already
        // Maxes to groundedCap. Air Fall/Reach keeps 140°. Hip-unload
        // hold would still be a no-op (ThighHold already owns the swing
        // hip on Fall via fold hold).
        // -walkPlantFallFoldGrowEvenFirst 0 restores the former 104°
        // grow cap on the first grounded Fall/Reach. Catch / later
        // cycles keep the envelope. Not trailing A2, not −swingHipFlex,
        // and not a PD retune.
        balance.walkPlantFallFoldGrowEvenFirst = GetFloatArg(
            "-walkPlantFallFoldGrowEvenFirst", balance.walkPlantFallFoldGrowEvenFirst);
        // Same Fall fifth quarter of the grounded fold. FoldGrowEvenFirst
        // wrote 113° (Lerp 104→140 at 0.25). Leftover-cap grow through
        // AcrossFirst is spent: 0.045° short of sci_a, ~0.011 cm of a
        // 0.43 m thigh, fade stuck at 0.972 so liftSignal stays −0.945.
        // DualLoadMinSpan never opens: that is the lift phase. Knee
        // torque is thigh–shin, so more fold shortens the swing chain
        // without asking the hip past 40°. Same quarter as fold grow
        // (Lerp 113 → 140 at 0.25 = 120°, 2·L·cos(60°) ≈ 0.50 L vs
        // 2·L·cos(56.5°) ≈ 0.55 L, ~4 cm of a 0.86 m chain). Seed already
        // Maxes to groundedCap. Air Fall/Reach keeps 140°. Hip-unload
        // hold would still be a no-op (ThighHold already owns the swing
        // hip on Fall via fold hold).
        // -walkPlantFallFoldGrowStillFirst 0 restores the former 113°
        // grow cap on the first grounded Fall/Reach. Catch / later
        // cycles keep the envelope. Not trailing A2, not −swingHipFlex,
        // and not a PD retune.
        balance.walkPlantFallFoldGrowStillFirst = GetFloatArg(
            "-walkPlantFallFoldGrowStillFirst", balance.walkPlantFallFoldGrowStillFirst);
        // Same Fall sixth quarter of the grounded fold. FoldGrowStillFirst
        // wrote 120° (Lerp 113→140 at 0.25). Leftover-cap grow through
        // AcrossFirst is spent: 0.045° short of sci_a, ~0.011 cm of a
        // 0.43 m thigh, fade stuck at 0.972 so liftSignal stays −0.945.
        // DualLoadMinSpan never opens: that is the lift phase. Knee
        // torque is thigh–shin, so more fold shortens the swing chain
        // without asking the hip past 40°. Same quarter as fold grow
        // (Lerp 120 → 140 at 0.25 = 125°, 2·L·cos(62.5°) ≈ 0.46 L vs
        // 2·L·cos(60°) ≈ 0.50 L, ~3 cm of a 0.86 m chain). Seed already
        // Maxes to groundedCap. Air Fall/Reach keeps 140°. Hip-unload
        // hold would still be a no-op (ThighHold already owns the swing
        // hip on Fall via fold hold).
        // -walkPlantFallFoldGrowAgainFirst 0 restores the former 120°
        // grow cap on the first grounded Fall/Reach. Catch / later
        // cycles keep the envelope. Not trailing A2, not −swingHipFlex,
        // and not a PD retune.
        balance.walkPlantFallFoldGrowAgainFirst = GetFloatArg(
            "-walkPlantFallFoldGrowAgainFirst", balance.walkPlantFallFoldGrowAgainFirst);
        // Same Fall seventh quarter of the grounded fold. FoldGrowAgainFirst
        // wrote 125° (Lerp 120→140 at 0.25). Leftover-cap grow through
        // AcrossFirst is spent: 0.045° short of sci_a, ~0.011 cm of a
        // 0.43 m thigh, fade stuck at 0.972 so liftSignal stays −0.945.
        // DualLoadMinSpan never opens: that is the lift phase. Knee
        // torque is thigh–shin, so more fold shortens the swing chain
        // without asking the hip past 40°. Same quarter as fold grow
        // (Lerp 125 → 140 at 0.25 = 129°, 2·L·cos(64.5°) ≈ 0.43 L vs
        // 2·L·cos(62.5°) ≈ 0.46 L, ~3 cm of a 0.86 m chain). Seed already
        // Maxes to groundedCap. Air Fall/Reach keeps 140°. Hip-unload
        // hold would still be a no-op (ThighHold already owns the swing
        // hip on Fall via fold hold).
        // -walkPlantFallFoldGrowAlsoFirst 0 restores the former 125°
        // grow cap on the first grounded Fall/Reach. Catch / later
        // cycles keep the envelope. Not trailing A2, not −swingHipFlex,
        // and not a PD retune.
        balance.walkPlantFallFoldGrowAlsoFirst = GetFloatArg(
            "-walkPlantFallFoldGrowAlsoFirst", balance.walkPlantFallFoldGrowAlsoFirst);
        // Same Fall eighth quarter of the grounded fold. FoldGrowAlsoFirst
        // wrote 129° (Lerp 125→140 at 0.25). Leftover-cap grow through
        // AcrossFirst is spent: 0.045° short of sci_a, ~0.011 cm of a
        // 0.43 m thigh, fade stuck at 0.972 so liftSignal stays −0.945.
        // DualLoadMinSpan never opens: that is the lift phase. Knee
        // torque is thigh–shin, so more fold shortens the swing chain
        // without asking the hip past 40°. Same quarter as fold grow
        // (Lerp 129 → 140 at 0.25 = 132°, 2·L·cos(66°) ≈ 0.41 L vs
        // 2·L·cos(64.5°) ≈ 0.43 L, ~2 cm of a 0.86 m chain). Seed already
        // Maxes to groundedCap. Air Fall/Reach keeps 140°. Hip-unload
        // hold would still be a no-op (ThighHold already owns the swing
        // hip on Fall via fold hold).
        // -walkPlantFallFoldGrowExtraFirst 0 restores the former 129°
        // grow cap on the first grounded Fall/Reach. Catch / later
        // cycles keep the envelope. Not trailing A2, not −swingHipFlex,
        // and not a PD retune.
        balance.walkPlantFallFoldGrowExtraFirst = GetFloatArg(
            "-walkPlantFallFoldGrowExtraFirst", balance.walkPlantFallFoldGrowExtraFirst);
        // WALK-2 initiation is knee lift + stance drive + forward lean.
        // Leftover-cap grow through AcrossFirst is spent: 0.045° short
        // of sci_a, ~0.011 cm of a 0.43 m thigh, fade stuck at 0.972
        // so liftSignal stays −0.945. DualLoadMinSpan never opens:
        // that is the lift phase. Knee torque is thigh–shin, so more
        // fold shortens the swing chain without asking the hip past 40°.
        // Same quarter as fold grow (Lerp 132 → 140 at 0.25 = 134°,
        // 2·L·cos(67°) ≈ 0.39 L vs 2·L·cos(66°) ≈ 0.41 L, ~1 cm of a
        // 0.86 m chain). Seed already Maxes to groundedCap. Air Fall/Reach
        // keeps 140°. Hip-unload hold would still be a no-op (ThighHold
        // already owns the swing hip on Fall via fold hold).
        // -walkPlantFallFoldGrowPlusFirst 0 restores the former 132°
        // grow cap on the first grounded Fall/Reach. Catch / later
        // cycles keep the envelope. Not trailing A2, not −swingHipFlex,
        // and not a PD retune.
        balance.walkPlantFallFoldGrowPlusFirst = GetFloatArg(
            "-walkPlantFallFoldGrowPlusFirst", balance.walkPlantFallFoldGrowPlusFirst);
        // WALK-2 initiation is knee lift + stance drive + forward lean.
        // Leftover-cap grow through AcrossFirst is spent: 0.045° short
        // of sci_a, ~0.011 cm of a 0.43 m thigh, fade stuck at 0.972
        // so liftSignal stays −0.945. DualLoadMinSpan never opens:
        // that is the lift phase. Knee torque is thigh–shin, so more
        // fold shortens the swing chain without asking the hip past 40°.
        // Same quarter as fold grow (Lerp 134 → 140 at 0.25 = 136°,
        // 2·L·cos(68°) ≈ 0.37 L vs 2·L·cos(67°) ≈ 0.39 L, ~1 cm of a
        // 0.86 m chain). Seed already Maxes to groundedCap. Air Fall/Reach
        // keeps 140°. Hip-unload hold would still be a no-op (ThighHold
        // already owns the swing hip on Fall via fold hold).
        // -walkPlantFallFoldGrowFurtherFirst 0 restores the former 134°
        // grow cap on the first grounded Fall/Reach. Catch / later
        // cycles keep the envelope. Not trailing A2, not −swingHipFlex,
        // and not a PD retune.
        balance.walkPlantFallFoldGrowFurtherFirst = GetFloatArg(
            "-walkPlantFallFoldGrowFurtherFirst", balance.walkPlantFallFoldGrowFurtherFirst);
        // WALK-2 initiation is knee lift + stance drive + forward lean.
        // Leftover-cap grow through AcrossFirst is spent: 0.045° short
        // of sci_a, ~0.011 cm of a 0.43 m thigh, fade stuck at 0.972
        // so liftSignal stays −0.945. DualLoadMinSpan never opens:
        // that is the lift phase. Knee torque is thigh–shin, so more
        // fold shortens the swing chain without asking the hip past 40°.
        // Same quarter as fold grow (Lerp 136 → 140 at 0.25 = 137°,
        // 2·L·cos(68.5°) ≈ 0.37 L vs 2·L·cos(68°) ≈ 0.37 L, ~1 cm of a
        // 0.86 m chain). Seed already Maxes to groundedCap. Air Fall/Reach
        // keeps 140°. Hip-unload hold would still be a no-op (ThighHold
        // already owns the swing hip on Fall via fold hold).
        // -walkPlantFallFoldGrowOnwardFirst 0 restores the former 136°
        // grow cap on the first grounded Fall/Reach. Catch / later
        // cycles keep the envelope. Not trailing A2, not −swingHipFlex,
        // and not a PD retune.
        balance.walkPlantFallFoldGrowOnwardFirst = GetFloatArg(
            "-walkPlantFallFoldGrowOnwardFirst", balance.walkPlantFallFoldGrowOnwardFirst);
        // WALK-2 initiation is knee lift + stance drive + forward lean.
        // Leftover-cap grow through AcrossFirst is spent: 0.045° short
        // of sci_a, ~0.011 cm of a 0.43 m thigh, fade stuck at 0.972
        // so liftSignal stays −0.945. DualLoadMinSpan never opens:
        // that is the lift phase. Knee torque is thigh–shin, so more
        // fold shortens the swing chain without asking the hip past 40°.
        // Same quarter as fold grow (Lerp 137 → 140 at 0.25 = 137.75°
        // → 138°, 2·L·cos(69°) ≈ 0.36 L vs 2·L·cos(68.5°) ≈ 0.37 L,
        // ~0.7 cm of a 0.86 m chain). Seed already Maxes to groundedCap.
        // Air Fall/Reach keeps 140°. Hip-unload hold would still be a
        // no-op (ThighHold already owns the swing hip on Fall via
        // fold hold).
        // -walkPlantFallFoldGrowAheadFirst 0 restores the former 137°
        // grow cap on the first grounded Fall/Reach. Catch / later
        // cycles keep the envelope. Not trailing A2, not −swingHipFlex,
        // and not a PD retune.
        balance.walkPlantFallFoldGrowAheadFirst = GetFloatArg(
            "-walkPlantFallFoldGrowAheadFirst", balance.walkPlantFallFoldGrowAheadFirst);
        // WALK-2 initiation is knee lift + stance drive + forward lean.
        // Leftover-cap grow through AcrossFirst is spent: 0.045° short
        // of sci_a, ~0.011 cm of a 0.43 m thigh, fade stuck at 0.972
        // so liftSignal stays −0.945. DualLoadMinSpan never opens:
        // that is the lift phase. Knee torque is thigh–shin, so more
        // fold shortens the swing chain without asking the hip past 40°.
        // Same quarter as fold grow (Lerp 138 → 140 at 0.25 = 138.5°
        // → 139°, 2·L·cos(69.5°) ≈ 0.35 L vs 2·L·cos(69°) ≈ 0.36 L,
        // ~0.7 cm of a 0.86 m chain). Seed already Maxes to groundedCap.
        // Air Fall/Reach keeps 140°. Hip-unload hold would still be a
        // no-op (ThighHold already owns the swing hip on Fall via
        // fold hold).
        // -walkPlantFallFoldGrowAlongFirst 0 restores the former 138°
        // grow cap on the first grounded Fall/Reach. Catch / later
        // cycles keep the envelope. Not trailing A2, not −swingHipFlex,
        // and not a PD retune.
        balance.walkPlantFallFoldGrowAlongFirst = GetFloatArg(
            "-walkPlantFallFoldGrowAlongFirst", balance.walkPlantFallFoldGrowAlongFirst);
        // Same Fall grow of the grounded fold a fourteenth quarter.
        // Leftover-cap grow through AcrossFirst is spent (0.045° short
        // of sci_a, ~0.011 cm of a 0.43 m thigh, fade stuck at 0.972
        // so liftSignal stays −0.945. DualLoadMinSpan never opens:
        // that is the lift phase. Knee torque is thigh–shin, so more
        // fold shortens the swing chain without asking the hip past 40°.
        // Same quarter as fold grow (Lerp 139 → 140 at 0.25 = 139.25°
        // → 139°, 2·L·cos(69.625°) ≈ 0.35 L vs 2·L·cos(69.5°) ≈ 0.35 L,
        // ~0.2 cm of a 0.86 m chain). Seed already Maxes to groundedCap.
        // Air Fall/Reach keeps 140°. Hip-unload hold would still be a
        // no-op (ThighHold already owns the swing hip on Fall via
        // fold hold).
        // -walkPlantFallFoldGrowCloseFirst 0 restores the former 139°
        // grow cap on the first grounded Fall/Reach. Catch / later
        // cycles keep the envelope. Not trailing A2, not −swingHipFlex,
        // and not a PD retune.
        balance.walkPlantFallFoldGrowCloseFirst = GetFloatArg(
            "-walkPlantFallFoldGrowCloseFirst", balance.walkPlantFallFoldGrowCloseFirst);
        // Same Fall grow of the grounded fold a fifteenth quarter.
        // Leftover-cap grow through AcrossFirst is spent (0.045° short
        // of sci_a, ~0.011 cm of a 0.43 m thigh, fade stuck at 0.972
        // so liftSignal stays −0.945. DualLoadMinSpan never opens:
        // that is the lift phase. Knee torque is thigh–shin, so more
        // fold shortens the swing chain without asking the hip past 40°.
        // Same quarter as fold grow (Lerp 139.25 → 140 at 0.25 = 139.44°
        // → 139.5°, 2·L·cos(69.72°) ≈ 0.35 L vs 2·L·cos(69.625°) ≈ 0.35 L,
        // ~0.1 cm of a 0.86 m chain). Seed already Maxes to groundedCap.
        // Air Fall/Reach keeps 140°. Hip-unload hold would still be a
        // no-op (ThighHold already owns the swing hip on Fall via
        // fold hold).
        // -walkPlantFallFoldGrowNearFirst 0 restores the former 139.25°
        // grow cap on the first grounded Fall/Reach. Catch / later
        // cycles keep the envelope. Not trailing A2, not −swingHipFlex,
        // and not a PD retune.
        balance.walkPlantFallFoldGrowNearFirst = GetFloatArg(
            "-walkPlantFallFoldGrowNearFirst", balance.walkPlantFallFoldGrowNearFirst);
        // Same Fall pin of the grounded fold to reachKneeFlex. Nested
        // 0.25 from Plant 55° is ~138.86°, not the rounded 139.5°.
        // Leftover-cap grow through AcrossFirst is spent: 0.045° short
        // of sci_a, ~0.011 cm of a 0.43 m thigh, fade stuck at 0.972
        // so liftSignal stays −0.945. DualLoadMinSpan never opens:
        // that is the lift phase. Knee torque is thigh–shin, so the
        // pin shortens the swing chain without asking the hip past 40°.
        // 2·L·cos(70°) ≈ 0.34 L vs 2·L·cos(69.43°) ≈ 0.35 L, ~0.8 cm
        // of a 0.86 m chain. Seed already Maxes to groundedCap. Air
        // Fall/Reach already keeps 140°. Hip-unload hold would still
        // be a no-op (ThighHold already owns the swing hip on Fall via
        // fold hold).
        // -walkPlantFallFoldGrowReachFirst 0 restores the former nested
        // ~138.86° grow cap on the first grounded Fall/Reach. Catch /
        // later cycles keep the envelope. Not trailing A2, not
        // −swingHipFlex, and not a PD retune.
        balance.walkPlantFallFoldGrowReachFirst = GetFloatArg(
            "-walkPlantFallFoldGrowReachFirst", balance.walkPlantFallFoldGrowReachFirst);
        // Same Fall grow of the stance-hip drive load. DualLoadMinSpan
        // never opens, so the held walkStanceHipExtend still multiplies
        // the 0.5 floor: 0.15×0.5 = 0.075 of the 0.25 clamp — half the
        // pelvis-forward the 140° pin needs. Opening DualLoad to feed
        // that unload would Lerp dualSupportStandCap 0.40→0.55 at
        // blend 0.25 (the recorded cap fold). Same quarter on the load
        // term only (Lerp 0.5→1 at 0.25 = 0.625, drive 0.09375). Plant
        // clock 1 already writes it so EnterFall is not a step.
        // firstRoom still fades at plantComLeadMax.
        // -walkPlantFallStanceDriveGrowFirst 0 restores the former
        // 0.5-floor drive under the 140° shin. Catch / later cycles
        // keep DualLoad / pushAfterAir. Not leftover-cap, not
        // Clamp01, not another millimetre 0.25, not trailing A2, not
        // −swingHipFlex, not a DualLoad open, and not a PD retune.
        balance.walkPlantFallStanceDriveGrowFirst = GetFloatArg(
            "-walkPlantFallStanceDriveGrowFirst", balance.walkPlantFallStanceDriveGrowFirst);
        // Same Fall grow of the stance-hip drive load a second quarter.
        // DualLoadMinSpan never opens, so GrowFirst still multiplies
        // 0.625: 0.15×0.625 = 0.09375 of the 0.25 clamp — short of the
        // pelvis-forward the 140° pin needs. Opening DualLoad to feed
        // that unload would Lerp dualSupportStandCap 0.40→0.55 at
        // blend 0.25 (the recorded cap fold). Same quarter on the load
        // term only (Lerp 0.625→1 at 0.25 = 0.719, drive 0.1078). Plant
        // clock 1 already writes it so EnterFall is not a step.
        // firstRoom still fades at plantComLeadMax.
        // -walkPlantFallStanceDriveGrowMoreFirst 0 restores the former
        // 0.625-load drive under the 140° shin. Catch / later cycles
        // keep DualLoad / pushAfterAir. Not leftover-cap, not
        // Clamp01, not another millimetre 0.25, not trailing A2, not
        // −swingHipFlex, not a DualLoad open, and not a PD retune.
        balance.walkPlantFallStanceDriveGrowMoreFirst = GetFloatArg(
            "-walkPlantFallStanceDriveGrowMoreFirst", balance.walkPlantFallStanceDriveGrowMoreFirst);
        // Same Fall grow of the stance-hip drive load a third quarter.
        // DualLoadMinSpan never opens, so GrowMoreFirst still multiplies
        // 0.719: 0.15×0.719 = 0.1078 of the 0.25 clamp — short of the
        // pelvis-forward the 140° pin needs. Opening DualLoad to feed
        // that unload would Lerp dualSupportStandCap 0.40→0.55 at
        // blend 0.25 (the recorded cap fold). Same quarter on the load
        // term only (Lerp 0.719→1 at 0.25 = 0.789, drive 0.1184). Plant
        // clock 1 already writes it so EnterFall is not a step.
        // firstRoom still fades at plantComLeadMax.
        // -walkPlantFallStanceDriveGrowStillFirst 0 restores the former
        // 0.719-load drive under the 140° shin. Catch / later cycles
        // keep DualLoad / pushAfterAir. Not leftover-cap, not
        // Clamp01, not another millimetre 0.25, not trailing A2, not
        // −swingHipFlex, not a DualLoad open, and not a PD retune.
        balance.walkPlantFallStanceDriveGrowStillFirst = GetFloatArg(
            "-walkPlantFallStanceDriveGrowStillFirst", balance.walkPlantFallStanceDriveGrowStillFirst);
        // Same Fall grow of the stance-hip drive load a fourth quarter.
        // DualLoadMinSpan never opens, so GrowStillFirst still multiplies
        // 0.789: 0.15×0.789 = 0.1184 of the 0.25 clamp — short of the
        // pelvis-forward the 140° pin needs. Opening DualLoad to feed
        // that unload would Lerp dualSupportStandCap 0.40→0.55 at
        // blend 0.25 (the recorded cap fold). Pinning 0.789→1.0 is a
        // 27% drive jump; leftover is not millimetres yet. Same quarter
        // on the load term only (Lerp 0.789→1 at 0.25 = 0.842, drive
        // 0.1263). Plant clock 1 already writes it so EnterFall is not
        // a step. firstRoom still fades at plantComLeadMax.
        // -walkPlantFallStanceDriveGrowYetFirst 0 restores the former
        // 0.789-load drive under the 140° shin. Catch / later cycles
        // keep DualLoad / pushAfterAir. Not leftover-cap, not
        // Clamp01, not another millimetre 0.25, not trailing A2, not
        // −swingHipFlex, not a DualLoad open, and not a PD retune.
        balance.walkPlantFallStanceDriveGrowYetFirst = GetFloatArg(
            "-walkPlantFallStanceDriveGrowYetFirst", balance.walkPlantFallStanceDriveGrowYetFirst);
        // Same Fall grow of the stance-hip drive load a fifth quarter.
        // DualLoadMinSpan never opens, so GrowYetFirst still multiplies
        // 0.842: 0.15×0.842 = 0.1263 of the 0.25 clamp — short of the
        // pelvis-forward the 140° pin needs. Opening DualLoad to feed
        // that unload would Lerp dualSupportStandCap 0.40→0.55 at
        // blend 0.25 (the recorded cap fold). Pinning 0.842→1.0 is a
        // 19% drive jump; leftover is not millimetres yet. Same quarter
        // on the load term only (Lerp 0.842→1 at 0.25 = 0.881, drive
        // 0.1322). Plant clock 1 already writes it so EnterFall is not
        // a step. firstRoom still fades at plantComLeadMax.
        // -walkPlantFallStanceDriveGrowFarFirst 0 restores the former
        // 0.842-load drive under the 140° shin. Catch / later cycles
        // keep DualLoad / pushAfterAir. Not leftover-cap, not
        // Clamp01, not another millimetre 0.25, not trailing A2, not
        // −swingHipFlex, not a DualLoad open, and not a PD retune.
        balance.walkPlantFallStanceDriveGrowFarFirst = GetFloatArg(
            "-walkPlantFallStanceDriveGrowFarFirst", balance.walkPlantFallStanceDriveGrowFarFirst);
        // Same Fall grow of the stance-hip drive load a sixth quarter.
        // DualLoadMinSpan never opens, so GrowFarFirst still multiplies
        // 0.881: 0.15×0.881 = 0.1322 of the 0.25 clamp — short of the
        // pelvis-forward the 140° pin needs. Opening DualLoad to feed
        // that unload would Lerp dualSupportStandCap 0.40→0.55 at
        // blend 0.25 (the recorded cap fold). Pinning 0.881→1.0 is a
        // 13% drive jump; leftover is not millimetres yet. Same quarter
        // on the load term only (Lerp 0.881→1 at 0.25 = 0.911, drive
        // 0.1367). Plant clock 1 already writes it so EnterFall is not
        // a step. firstRoom still fades at plantComLeadMax.
        // -walkPlantFallStanceDriveGrowFartherFirst 0 restores the former
        // 0.881-load drive under the 140° shin. Catch / later cycles
        // keep DualLoad / pushAfterAir. Not leftover-cap, not
        // Clamp01, not another millimetre 0.25, not trailing A2, not
        // −swingHipFlex, not a DualLoad open, and not a PD retune.
        balance.walkPlantFallStanceDriveGrowFartherFirst = GetFloatArg(
            "-walkPlantFallStanceDriveGrowFartherFirst", balance.walkPlantFallStanceDriveGrowFartherFirst);
        // Same Fall grow of the stance-hip drive load a seventh quarter.
        // DualLoadMinSpan never opens, so GrowFartherFirst still multiplies
        // 0.911: 0.15×0.911 = 0.1367 of the 0.25 clamp — short of the
        // pelvis-forward the 140° pin needs. Opening DualLoad to feed
        // that unload would Lerp dualSupportStandCap 0.40→0.55 at
        // blend 0.25 (the recorded cap fold). Pinning 0.911→1.0 is a
        // 10% drive jump; leftover is not millimetres yet. Same quarter
        // on the load term only (Lerp 0.911→1 at 0.25 = 0.933, drive
        // 0.1400). Plant clock 1 already writes it so EnterFall is not
        // a step. firstRoom still fades at plantComLeadMax.
        // -walkPlantFallStanceDriveGrowFarthestFirst 0 restores the former
        // 0.911-load drive under the 140° shin. Catch / later cycles
        // keep DualLoad / pushAfterAir. Not leftover-cap, not
        // Clamp01, not another millimetre 0.25, not trailing A2, not
        // −swingHipFlex, not a DualLoad open, and not a PD retune.
        balance.walkPlantFallStanceDriveGrowFarthestFirst = GetFloatArg(
            "-walkPlantFallStanceDriveGrowFarthestFirst", balance.walkPlantFallStanceDriveGrowFarthestFirst);
        // Same Fall grow of the stance-hip drive load an eighth quarter.
        // DualLoadMinSpan never opens, so GrowFarthestFirst still multiplies
        // 0.933: 0.15×0.933 = 0.1400 of the 0.25 clamp — short of the
        // pelvis-forward the 140° pin needs. Opening DualLoad to feed
        // that unload would Lerp dualSupportStandCap 0.40→0.55 at
        // blend 0.25 (the recorded cap fold). Pinning 0.933→1.0 is a
        // 7% drive jump; leftover is not millimetres yet. Same quarter
        // on the load term only (Lerp 0.933→1 at 0.25 = 0.950, drive
        // 0.1425). Plant clock 1 already writes it so EnterFall is not
        // a step. firstRoom still fades at plantComLeadMax.
        // -walkPlantFallStanceDriveGrowOutFirst 0 restores the former
        // 0.933-load drive under the 140° shin. Catch / later cycles
        // keep DualLoad / pushAfterAir. Not leftover-cap, not
        // Clamp01, not another millimetre 0.25, not trailing A2, not
        // −swingHipFlex, not a DualLoad open, and not a PD retune.
        balance.walkPlantFallStanceDriveGrowOutFirst = GetFloatArg(
            "-walkPlantFallStanceDriveGrowOutFirst", balance.walkPlantFallStanceDriveGrowOutFirst);
        // Same Fall grow of the stance-hip drive load a ninth quarter.
        // DualLoadMinSpan never opens, so GrowOutFirst still multiplies
        // 0.950: 0.15×0.950 = 0.1425 of the 0.25 clamp — short of the
        // pelvis-forward the 140° pin needs. Opening DualLoad to feed
        // that unload would Lerp dualSupportStandCap 0.40→0.55 at
        // blend 0.25 (the recorded cap fold). Pinning 0.950→1.0 is a
        // 5% drive jump; leftover is not millimetres yet. Same quarter
        // on the load term only (Lerp 0.950→1 at 0.25 = 0.962, drive
        // 0.1444). Plant clock 1 already writes it so EnterFall is not
        // a step. firstRoom still fades at plantComLeadMax.
        // -walkPlantFallStanceDriveGrowOutMoreFirst 0 restores the former
        // 0.950-load drive under the 140° shin. Catch / later cycles
        // keep DualLoad / pushAfterAir. Not leftover-cap, not
        // Clamp01, not another millimetre 0.25, not trailing A2, not
        // −swingHipFlex, not a DualLoad open, and not a PD retune.
        balance.walkPlantFallStanceDriveGrowOutMoreFirst = GetFloatArg(
            "-walkPlantFallStanceDriveGrowOutMoreFirst", balance.walkPlantFallStanceDriveGrowOutMoreFirst);
        // Same Fall grow of the stance-hip drive load a tenth quarter.
        // DualLoadMinSpan never opens, so GrowOutMoreFirst still multiplies
        // 0.962: 0.15×0.962 = 0.1444 of the 0.25 clamp — short of the
        // pelvis-forward the 140° pin needs. Opening DualLoad to feed
        // that unload would Lerp dualSupportStandCap 0.40→0.55 at
        // blend 0.25 (the recorded cap fold). Pinning 0.962→1.0 is a
        // 4% drive jump; leftover is not millimetres yet. Same quarter
        // on the load term only (Lerp 0.962→1 at 0.25 = 0.972, drive
        // 0.1457). Plant clock 1 already writes it so EnterFall is not
        // a step. firstRoom still fades at plantComLeadMax.
        // -walkPlantFallStanceDriveGrowOutStillFirst 0 restores the former
        // 0.962-load drive under the 140° shin. Catch / later cycles
        // keep DualLoad / pushAfterAir. Not leftover-cap, not
        // Clamp01, not another millimetre 0.25, not trailing A2, not
        // −swingHipFlex, not a DualLoad open, and not a PD retune.
        balance.walkPlantFallStanceDriveGrowOutStillFirst = GetFloatArg(
            "-walkPlantFallStanceDriveGrowOutStillFirst", balance.walkPlantFallStanceDriveGrowOutStillFirst);
        // Keep the Plant swing fold/lift across Plant→Fall (PhaseAge reset).
        // -walkPlantFallFoldHold 0 restores the former Fall start at 0.
        balance.walkPlantFallFoldHold = GetFloatArg(
            "-walkPlantFallFoldHold", balance.walkPlantFallFoldHold);
        balance.walkScrapeAnkleDrive = GetFloatArg("-walkScrapeAnkleDrive", balance.walkScrapeAnkleDrive);
        balance.walkPushToeFade = GetFloatArg("-walkPushToeFade", balance.walkPushToeFade);
        // Fall CoM setpoint roll across the sole. 0 = former fixed fallComLead.
        balance.walkFallComRollToe = GetFloatArg("-walkFallComRoll", balance.walkFallComRollToe);
        balance.walkTargetSpeed = GetFloatArg("-walkTargetSpeed", balance.walkTargetSpeed);
        balance.walkSpeedPushGain = GetFloatArg("-walkSpeedPushGain", balance.walkSpeedPushGain);
        balance.walkSpeedPushMax = GetFloatArg("-walkSpeedPushMax", balance.walkSpeedPushMax);
        balance.walkPushCounterAllowance = GetFloatArg("-walkPushCounterAllowance", balance.walkPushCounterAllowance);
        balance.walkPropulsionGain = GetFloatArg("-walkPropulsionGain", balance.walkPropulsionGain);
        balance.walkPropulsionMax = GetFloatArg("-walkPropulsionMax", balance.walkPropulsionMax);
        balance.walkPropulsionStart = GetFloatArg("-walkPropulsionStart", balance.walkPropulsionStart);
        balance.walkPropulsionPeak = GetFloatArg("-walkPropulsionPeak", balance.walkPropulsionPeak);
        balance.walkPropulsionEnd = GetFloatArg("-walkPropulsionEnd", balance.walkPropulsionEnd);
        balance.walkPropulsionTiltGuardDeg = GetFloatArg("-walkPropulsionTiltGuard", balance.walkPropulsionTiltGuardDeg);
        balance.walkPropulsionComWindow = GetFloatArg("-walkPropulsionComWindow", balance.walkPropulsionComWindow);
        balance.walkPropulsionMinWeight = GetFloatArg("-walkPropulsionMinWeight", balance.walkPropulsionMinWeight);
        balance.walkPropulsionCounterAllowance = GetFloatArg("-walkPropulsionCounterAllowance", balance.walkPropulsionCounterAllowance);
        balance.walkSpeedPushComGateScale = GetFloatArg("-walkSpeedPushComGateScale", balance.walkSpeedPushComGateScale);
        balance.walkXCoMWeight = GetFloatArg("-walkXCoMWeight", balance.walkXCoMWeight);
        balance.walkXCoMHeight = GetFloatArg("-walkXCoMHeight", balance.walkXCoMHeight);
        balance.walkComVelFilterTau = GetFloatArg("-walkComVelFilterTau", balance.walkComVelFilterTau);
        balance.walkSpeedLeanGain = GetFloatArg("-walkSpeedLeanGain", balance.walkSpeedLeanGain);
        balance.walkSpeedLeanMax = GetFloatArg("-walkSpeedLeanMax", balance.walkSpeedLeanMax);
        balance.walkSpeedLeanTorsoScale = GetFloatArg("-walkSpeedLeanTorsoScale", balance.walkSpeedLeanTorsoScale);
        balance.walkSpeedDriveGain = GetFloatArg("-walkSpeedDriveGain", balance.walkSpeedDriveGain);
        balance.walkSpeedDriveMax = GetFloatArg("-walkSpeedDriveMax", balance.walkSpeedDriveMax);
        balance.walkSpeedDriveStartLevel = GetFloatArg("-walkSpeedDriveStartLevel", balance.walkSpeedDriveStartLevel);
        balance.walkSpeedSwingHipGain = GetFloatArg("-walkSpeedSwingHipGain", balance.walkSpeedSwingHipGain);
        balance.walkSpeedSwingKneeGain = GetFloatArg("-walkSpeedSwingKneeGain", balance.walkSpeedSwingKneeGain);
        balance.walkSpeedSwingFlexMax = GetFloatArg("-walkSpeedSwingFlexMax", balance.walkSpeedSwingFlexMax);
        balance.walkSwingHipBoost = GetFloatArg("-walkSwingHipBoost", balance.walkSwingHipBoost);
        balance.walkSwingKneeBoost = GetFloatArg("-walkSwingKneeBoost", balance.walkSwingKneeBoost);
        balance.walkStepLength = GetFloatArg("-walkStepLength", balance.walkStepLength);
        balance.walkStepLengthSpeedGain = GetFloatArg("-walkStepLengthSpeedGain", balance.walkStepLengthSpeedGain);
        balance.walkStepPlacementGain = GetFloatArg("-walkStepPlacementGain", balance.walkStepPlacementGain);
        balance.walkStepPlacementMax = GetFloatArg("-walkStepPlacementMax", balance.walkStepPlacementMax);
        balance.walkStepPlacementGroundFraction = GetFloatArg("-walkStepPlacementGroundFraction", balance.walkStepPlacementGroundFraction);
        balance.walkStepComMargin = GetFloatArg("-walkStepComMargin", balance.walkStepComMargin);
        // Capture-point step target. 0 = former walkStepLength floor.
        balance.walkStepCaptureTarget = GetFloatArg("-walkStepCapture", balance.walkStepCaptureTarget);
        balance.walkStepMinLength = GetFloatArg("-walkStepMin", balance.walkStepMinLength);
        // Landing point also meets catchFootLead. 0 = former blind command.
        balance.walkStepCatchLeadSync = GetFloatArg("-walkStepCatchSync", balance.walkStepCatchLeadSync);
        balance.walkStepCatchLeadPad = GetFloatArg("-walkStepCatchPad", balance.walkStepCatchLeadPad);
        balance.walkPlacementSyncStart = GetFloatArg("-walkPlacementSyncStart", balance.walkPlacementSyncStart);
        balance.walkTouchdownSyncClearance = GetFloatArg("-walkTouchdownSyncClearance", balance.walkTouchdownSyncClearance);
        // Touchdown velocity match. 0 = former forward-travelling plant.
        balance.walkSwingRetractGain = GetFloatArg("-walkSwingRetract", balance.walkSwingRetractGain);
        balance.walkSwingRetractMax = GetFloatArg("-walkSwingRetractMax", balance.walkSwingRetractMax);
        // Geometric swing placement. 0 = former constant swingHipFlex reach.
        balance.walkSwingPlaceGeometry = GetFloatArg("-walkSwingPlaceGeo", balance.walkSwingPlaceGeometry);
        // Solve owns the air hip target. 0 = former placement boost stacked on top.
        balance.walkSwingPlaceGeoOnly = GetFloatArg("-walkSwingPlaceGeoOnly", balance.walkSwingPlaceGeoOnly);
        // Solve aims the foot centre, not the ankle hinge (one heel offset
        // shorter). 0 = former hinge-referenced target.
        balance.walkSwingPlaceFootRef = GetFloatArg("-walkSwingPlaceFootRef", balance.walkSwingPlaceFootRef);
        // Geometric landing knee. 0 = former constant reachKneeLandDeg.
        balance.walkSwingLandKneeGeometry = GetFloatArg("-walkLandKneeGeo", balance.walkSwingLandKneeGeometry);
        balance.walkLandKneeMinDeg = GetFloatArg("-walkLandKneeMin", balance.walkLandKneeMinDeg);
        // Stacked dual-support: geometric swing-knee clearance + stance
        // knee 0. Not DualLoad, not ankle-zero, not a pelvis fight.
        // Clearance default 0.08: probe is 0.06, so 0.04 never un-grounds.
        // Also WALK-1 Stance (DrivePelvis off the planted swing).
        // -walkGeoSwingLift 0 restores the former fold-clock path.
        balance.walkGeoSwingLift = GetFloatArg("-walkGeoSwingLift", balance.walkGeoSwingLift);
        balance.walkGeoSwingLiftClearance = GetFloatArg(
            "-walkGeoSwingLiftClearance", balance.walkGeoSwingLiftClearance);
        // WALK-1: fade the swing hip/knee weld on that same clock.
        // 0 = former K=12 stopTorque, geo knee unused.
        balance.walkStanceGeoFriction = GetFloatArg(
            "-walkStanceGeoFriction", balance.walkStanceGeoFriction);
        // Cycle-0 stance-ankle push with the hip drive. 0 = former
        // pushWindow wait (stacked feet never plantarflex).
        balance.walkPlantStancePushFirst = GetFloatArg(
            "-walkPlantStancePushFirst", balance.walkPlantStancePushFirst);
        // Folded/light swing placement ahead of CoM. 0 = former grounded × 0.
        balance.walkStepPlaceOnFold = GetFloatArg(
            "-walkStepPlaceOnFold", balance.walkStepPlaceOnFold);
        // Swing-leg proportional band. <= errorReferenceDegrees (10) = former
        // shared band, i.e. a swing hip and knee saturated for the whole flight.
        balance.walkSwingErrorReferenceDegrees = GetFloatArg("-walkSwingErrorRef", balance.walkSwingErrorReferenceDegrees);
        // Swing-leg viscosity, seconds. 0 = former built weld on hip and knee.
        balance.walkSwingLegFrictionTau = GetFloatArg("-walkSwingFrictionTau", balance.walkSwingLegFrictionTau);
        // Swing-ankle viscosity, seconds. 0 = former built weld on the sole.
        // Independent of the line above on purpose: the two can be measured
        // apart (-walkSwingFrictionTau 0 -walkSwingAnkleFrictionTau 0.10).
        balance.walkSwingAnkleFrictionTau = GetFloatArg(
            "-walkSwingAnkleFrictionTau", balance.walkSwingAnkleFrictionTau);
        // Unload-share on that same envelope. 0 = former weld until air.
        balance.walkSwingFrictionLoadShare = GetFloatArg(
            "-walkSwingFrictionLoadShare", balance.walkSwingFrictionLoadShare);
        // Dirt snap to tau K at unload 0.5. 0 = former linear K lerp.
        balance.walkSwingFrictionClampSnap = GetFloatArg(
            "-walkSwingFrictionClampSnap", balance.walkSwingFrictionClampSnap);
        // Hold that snap through first millimetres of air. 0 = 23:22 pulse.
        balance.walkSwingFrictionSnapHold = GetFloatArg(
            "-walkSwingFrictionSnapHold", balance.walkSwingFrictionSnapHold);
        // SPD denominator of that same sole. 0 = former flexor-only denominator
        // (plantarflex integrated as if the calf were the 22 N·m dorsiflexor).
        balance.walkSwingAnkleSpdTorque = GetFloatArg(
            "-walkSwingAnkleSpdTorque", balance.walkSwingAnkleSpdTorque);
        balance.walkStanceKneeBend = GetFloatArg("-walkStanceKneeBend", balance.walkStanceKneeBend);
        // Schedule of that bend on the fall-catch gait. 0 = the former
        // schedule that GREW the bend as the swing foot cleared.
        balance.walkStanceKneeBendLiftFade = GetFloatArg(
            "-walkStanceBendFade", balance.walkStanceKneeBendLiftFade);
        // Fraction of kneeRecoveryFlex left on the fall-catch stance knee.
        // 1 = former behaviour (the quiet-stance reflex on the loaded strut).
        balance.walkStanceKneeRecoveryFlex = GetFloatArg(
            "-walkStanceKneeRecovery", balance.walkStanceKneeRecoveryFlex);
        balance.walkForwardPelvisLean = GetFloatArg("-walkForwardPelvisLean", balance.walkForwardPelvisLean);
        balance.walkForwardTorsoLean = GetFloatArg("-walkForwardTorsoLean", balance.walkForwardTorsoLean);
        balance.walkStanceExtend = GetFloatArg("-walkStanceExtend", balance.walkStanceExtend);
        balance.walkComLeadX = GetFloatArg("-walkComLead", balance.walkComLeadX);
        // First-transfer: geo-lift waits for this CoM lead on the named
        // stance sole. 0 = former idle-clock lift on stacked feet.
        balance.walkFirstLiftLead = GetFloatArg(
            "-walkFirstLiftLead", balance.walkFirstLiftLead);
        balance.walkFirstLeadSeconds = GetFloatArg(
            "-walkFirstLeadSeconds", balance.walkFirstLeadSeconds);
        balance.useWalkLift = Mathf.RoundToInt(GetFloatArg("-walkLift", balance.useWalkLift));
        balance.walkLiftUnloadFromDirt = GetFloatArg(
            "-walkLiftDirt", balance.walkLiftUnloadFromDirt);
        balance.walkLiftKneeFlex = GetFloatArg("-walkLiftKnee", balance.walkLiftKneeFlex);
        balance.walkLiftStepLength = GetFloatArg("-walkLiftStep", balance.walkLiftStepLength);
        balance.walkLiftStepMax = GetFloatArg("-walkLiftStepMax", balance.walkLiftStepMax);
        balance.walkLiftAnkleFwdDriveMax = GetFloatArg("-walkLiftAnkleFwd", balance.walkLiftAnkleFwdDriveMax);
        balance.walkAnkleDorsiHold = GetFloatArg("-walkAnkleDorsiHold", balance.walkAnkleDorsiHold);
        balance.walkAnkleDorsiFade = GetFloatArg("-walkAnkleDorsiFade", balance.walkAnkleDorsiFade);
        balance.walkLiftCaptureLookahead = GetFloatArg("-walkLiftCapture", balance.walkLiftCaptureLookahead);
        balance.walkLiftClearHeight = GetFloatArg("-walkLiftClear", balance.walkLiftClearHeight);
        balance.walkLiftUnloadSeconds = GetFloatArg("-walkLiftUnloadT", balance.walkLiftUnloadSeconds);
        balance.walkLiftClearSeconds = GetFloatArg("-walkLiftClearT", balance.walkLiftClearSeconds);
        balance.walkLiftAdvanceSeconds = GetFloatArg("-walkLiftAdvanceT", balance.walkLiftAdvanceSeconds);
        balance.walkLiftAdvanceHipRamp = GetFloatArg("-walkLiftAdvanceRamp", balance.walkLiftAdvanceHipRamp);
        balance.walkLiftHipRateDeg = GetFloatArg("-walkLiftHipRate", balance.walkLiftHipRateDeg);
        balance.walkLiftCoupleHipShare = GetFloatArg("-walkLiftCoupleHip", balance.walkLiftCoupleHipShare);
        balance.walkLiftCoupleAnkleShare = GetFloatArg("-walkLiftCoupleAnkle", balance.walkLiftCoupleAnkleShare);
        balance.walkLiftCoupleHipMax = GetFloatArg("-walkLiftCoupleHipMax", balance.walkLiftCoupleHipMax);
        balance.walkLiftReactionCap = GetFloatArg("-walkLiftReactionCap", balance.walkLiftReactionCap);
        balance.walkLiftArmGain = GetFloatArg("-walkLiftArmGain", balance.walkLiftArmGain);
        balance.walkLiftHoldTrunk = GetFloatArg("-walkLiftHoldTrunk", balance.walkLiftHoldTrunk);
        balance.walkLiftStancePdScale = GetFloatArg("-walkLiftPdScale", balance.walkLiftStancePdScale);
        balance.fallComLead = fallComLead;
        balance.walkSwingScissorLevel = GetFloatArg("-walkSwingScissor", balance.walkSwingScissorLevel);
        balance.walkSwingScissorFlex = GetFloatArg("-walkSwingScissorFlex", balance.walkSwingScissorFlex);
        // Swing-hip reaction couple. 0 = former behaviour (pelvis eats the reaction).
        balance.walkSwingLiftCouple = GetFloatArg("-walkSwingLiftCouple", balance.walkSwingLiftCouple);
        balance.walkSwingLiftCoupleMax = GetFloatArg("-walkSwingLiftCoupleMax", balance.walkSwingLiftCoupleMax);
        balance.walkSwingLiftCoupleCapScale = GetFloatArg("-walkSwingLiftCoupleCap", balance.walkSwingLiftCoupleCapScale);
        balance.walkSwingAirHold = GetFloatArg("-walkSwingAirHold", balance.walkSwingAirHold);
        balance.walkSwingHeelPlant = GetFloatArg("-walkSwingHeelPlant", balance.walkSwingHeelPlant);
        balance.walkSwingUnlatchPlant = GetFloatArg("-walkSwingUnlatchPlant", balance.walkSwingUnlatchPlant);
        balance.walkSwingLatchAir = GetFloatArg("-walkSwingLatchAir", balance.walkSwingLatchAir);
        balance.walkSwingAirLevel = GetFloatArg("-walkSwingAirLevel", balance.walkSwingAirLevel);
        balance.walkArmShoulderSwing = GetFloatArg("-walkArmShoulder", balance.walkArmShoulderSwing);
        balance.walkArmElbowSwing = GetFloatArg("-walkArmElbow", balance.walkArmElbowSwing);
        balance.walkArmTransferCarry = GetFloatArg("-walkArmTransfer", balance.walkArmTransferCarry);
        balance.walkArmBalanceShoulderGain = GetFloatArg("-walkArmBalanceShoulderGain", balance.walkArmBalanceShoulderGain);
        balance.walkArmBalanceElbowGain = GetFloatArg("-walkArmBalanceElbowGain", balance.walkArmBalanceElbowGain);
        balance.walkArmLegTrack = GetFloatArg("-walkArmLegTrack", balance.walkArmLegTrack);
        balance.walkLatePushGain = GetFloatArg("-walkLatePushGain", balance.walkLatePushGain);
        balance.walkLatePushStart = GetFloatArg("-walkLatePushStart", balance.walkLatePushStart);
        balance.walkLatePushMax = GetFloatArg("-walkLatePushMax", balance.walkLatePushMax);
        balance.walkLatePushNeedsAir = GetFloatArg("-walkLatePushNeedsAir", balance.walkLatePushNeedsAir ? 1f : 0f) > 0.5f;
        balance.walkLatePushNeedsTouchdownWindow = GetFloatArg("-walkLatePushNeedsTouchdownWindow", balance.walkLatePushNeedsTouchdownWindow ? 1f : 0f) > 0.5f;
        balance.standLegRatePerSecond = GetFloatArg("-standLegRate", balance.standLegRatePerSecond);
        balance.standLegReleaseRatePerSecond = GetFloatArg("-standLegReleaseRate", balance.standLegReleaseRatePerSecond);

        // WALK-3 capture-point walker. Default on: it owns walk cadence.
        // -captureWalk 0 restores the closed WalkLift / Stance-clock path.
        // Only -walk 1 windows reach this walker at all.
        balance.useCaptureWalker = Mathf.RoundToInt(
            GetFloatArg("-captureWalk", balance.useCaptureWalker));
        balance.captureStepDuration = GetFloatArg("-captureStepT", balance.captureStepDuration);
        balance.captureStepSpeedAdapt = Mathf.RoundToInt(
            GetFloatArg("-captureStepAdapt", balance.captureStepSpeedAdapt));
        balance.captureComLandingScale = GetFloatArg("-captureComLanding", balance.captureComLandingScale);
        balance.captureSwingXShare = GetFloatArg("-captureSwingX", balance.captureSwingXShare);
        balance.captureTargetSpeed = GetFloatArg("-captureSpeed", balance.captureTargetSpeed);
        balance.captureRunSpeedScale = GetFloatArg("-captureRunScale", balance.captureRunSpeedScale);
        balance.captureSpeedGain = GetFloatArg("-captureKv", balance.captureSpeedGain);
        balance.captureLookahead = GetFloatArg("-captureLookahead", balance.captureLookahead);
        balance.captureStepMin = GetFloatArg("-captureStepMin", balance.captureStepMin);
        balance.captureStepMax = GetFloatArg("-captureStepMax", balance.captureStepMax);
        balance.captureSwingApex = GetFloatArg("-captureApex", balance.captureSwingApex);
        balance.captureReachMargin = GetFloatArg("-captureReach", balance.captureReachMargin);
        balance.captureStepBackMax = GetFloatArg("-captureStepBack", balance.captureStepBackMax);
        balance.captureAbandonPutDown = Mathf.RoundToInt(
            GetFloatArg("-capturePutDown", balance.captureAbandonPutDown));
        balance.captureFootLandLift = GetFloatArg("-captureLandLift", balance.captureFootLandLift);
        balance.captureFootPress = GetFloatArg("-captureFootPress", balance.captureFootPress);
        balance.captureFootPitchDeg = GetFloatArg("-captureFootPitch", balance.captureFootPitchDeg);
        balance.captureDoubleSupportMin = GetFloatArg("-captureDsMin", balance.captureDoubleSupportMin);
        balance.captureDoubleSupportMax = GetFloatArg("-captureDsMax", balance.captureDoubleSupportMax);
        balance.captureDoubleSupportShare = GetFloatArg("-captureDsShare", balance.captureDoubleSupportShare);
        balance.captureTouchdownFrames = Mathf.RoundToInt(
            GetFloatArg("-captureTdFrames", balance.captureTouchdownFrames));
        balance.captureTouchdownBlendSeconds = GetFloatArg("-captureTdBlend", balance.captureTouchdownBlendSeconds);
        balance.captureLandKneeFlex = GetFloatArg("-captureLandKnee", balance.captureLandKneeFlex);
        balance.captureStanceLegLength = GetFloatArg("-captureLegLength", balance.captureStanceLegLength);
        balance.captureLandLegLength = GetFloatArg("-captureLandLength", balance.captureLandLegLength);
        balance.captureEntryRampSeconds = GetFloatArg("-captureEntryRamp", balance.captureEntryRampSeconds);
        balance.captureLegPushCap = GetFloatArg("-captureLegPushCap", balance.captureLegPushCap);
        balance.captureSupportKneeExtendRate = GetFloatArg("-captureKneeRate", balance.captureSupportKneeExtendRate);
        // Loaded-hip ceiling, one number per ground arm: heel 0.065 m and toe
        // 0.195 m are not the same lever, so they are not the same cap.
        balance.captureStanceHipTorqueCap = GetFloatArg("-captureHipCap", balance.captureStanceHipTorqueCap);
        balance.captureStanceHipTorqueCapForward = GetFloatArg("-captureHipCapFwd", balance.captureStanceHipTorqueCapForward);
        balance.captureStancePlantPress = GetFloatArg("-captureStancePress", balance.captureStancePlantPress);
        balance.captureDoubleSupportHoldSeconds = GetFloatArg("-captureDsHold", balance.captureDoubleSupportHoldSeconds);
        balance.captureTrunkLeanDeg = GetFloatArg("-captureTrunkLean", balance.captureTrunkLeanDeg);
        balance.capturePelvisLeanDeg = GetFloatArg("-capturePelvisLean", balance.capturePelvisLeanDeg);
        balance.captureLeanPlantFraction = GetFloatArg("-captureLeanPlant", balance.captureLeanPlantFraction);
        balance.captureCopLead = GetFloatArg("-captureCopLead", balance.captureCopLead);
        balance.captureCopScale = GetFloatArg("-captureCopScale", balance.captureCopScale);
        balance.captureCopTravelBias = GetFloatArg("-captureCopTravel", balance.captureCopTravelBias);
        balance.captureLaunchSeconds = GetFloatArg("-captureLaunchT", balance.captureLaunchSeconds);
        balance.captureLaunchAnkleCap = GetFloatArg("-captureLaunchCap", balance.captureLaunchAnkleCap);
        balance.captureLaunchSpeed = GetFloatArg("-captureLaunchSpeed", balance.captureLaunchSpeed);
        balance.captureLaunchLead = GetFloatArg("-captureLaunchLead", balance.captureLaunchLead);
        balance.captureCatchPast = GetFloatArg("-captureCatchPast", balance.captureCatchPast);
        balance.toePGain = GetFloatArg("-toeP", balance.toePGain);
        balance.toeDGain = GetFloatArg("-toeD", balance.toeDGain);
        balance.toeClearanceDeg = GetFloatArg("-toeClear", balance.toeClearanceDeg);
        balance.captureToePushDeg = GetFloatArg("-captureToePush", balance.captureToePushDeg);
        balance.captureToeLeanGain = GetFloatArg("-captureToeLean", balance.captureToeLeanGain);
        balance.captureRecoveryMargin = GetFloatArg("-captureRecoverMargin", balance.captureRecoveryMargin);
        balance.captureRecoveryDuration = GetFloatArg("-captureRecoverT", balance.captureRecoveryDuration);
        balance.captureSwingOvertime = GetFloatArg("-captureOvertime", balance.captureSwingOvertime);
        balance.captureCollapseDrop = GetFloatArg("-captureCollapseDrop", balance.captureCollapseDrop);

        // Speed at which muscle activation catches the controller command.
        // 500 — almost instant; 10 — the muscle rises in ~0.1 s.
        balance.muscleActivationSpeed = GetFloatArg("-activationSpeed", balance.muscleActivationSpeed);
    }

    // Picture only: same CameraFollow lens as Play Mode. Do not set
    // orthographicSize or a Y shift here — LateUpdate owns the frame.
    // WaitForEndOfFrame lets that LateUpdate run before ReadPixels.
    private IEnumerator CaptureVisualFrame(string path)
    {
        if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
        {
            Debug.LogWarning("HeadlessTrial: -visual skipped under -nographics.");
            yield break;
        }

        Camera cam = EnsureVisualFollowCamera();
        if (cam == null)
            yield break;

        // req-246: the story camera paints its shot (and logs the row) here,
        // before the frame is rendered - row N and shot PNG N share one index.
        // req-265 steady: the shot only arms; the row is written after LateUpdate.
        if (level1StoryCam != null)
        {
            if (level1CamSteady)
                ArmSteadyLevel1CamShot(cam, path);
            else
                ApplyLevel1CamShot(cam, path);
        }

        yield return new WaitForEndOfFrame();

        if (level1CamSteady)
            FlushSteadyLevel1CamRow(cam);

        const int w = 1920;
        const int h = 1080;
        RenderTexture rt = RenderTexture.GetTemporary(w, h, 24);
        RenderTexture prevTarget = cam.targetTexture;
        cam.targetTexture = rt;
        cam.Render();
        RenderTexture prevActive = RenderTexture.active;
        RenderTexture.active = rt;
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, w, h), 0, 0, false);
        tex.Apply(false, false);
        RenderTexture.active = prevActive;
        cam.targetTexture = prevTarget;
        RenderTexture.ReleaseTemporary(rt);
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Destroy(tex);
        WriteFrameReadyMarker(path, w, h);
    }

    // Bind the trial human to CameraFollow. Size and Y come from the
    // component (frameFullBody), same as GameProcess SampleScene stand.
    private Camera EnsureVisualFollowCamera()
    {
        Camera cam = Camera.main;
        if (cam == null)
            cam = FindFirstObjectByType<Camera>();
        if (cam == null)
        {
            GameObject go = new GameObject("VisCam");
            cam = go.AddComponent<Camera>();
            go.tag = "MainCamera";
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = SceneLighting.NightSky;
        }

        cam.orthographic = true;
        CameraFollow follow = cam.GetComponent<CameraFollow>();
        if (follow == null)
            follow = cam.gameObject.AddComponent<CameraFollow>();
        if (camAuto && !level1CamSteady)
        {
            // Owner's rule (Yury, 25.09): the frame's edges are set by the OBJECTS' extent -
            // everything spawned in the scene stays in the picture and the lens zooms in
            // and out as the bodies move. Replaces every fixed lens below. Capture/live
            // only: no physics row reads a camera.
            follow.enabled = false;
            CameraAutoFit fit = cam.GetComponent<CameraAutoFit>();
            if (fit == null)
                fit = cam.gameObject.AddComponent<CameraAutoFit>();
            fit.margin = camAutoMargin;
            if (bird != null) fit.Bind(bird.transform);
            if (plantTree != null) fit.Bind(plantTree.transform);
            if (appleTree != null) fit.Bind(appleTree.transform);
            if (dog != null) fit.Bind(dog.transform);
            if (human != null) fit.Bind(human.transform);
            return cam;
        }
        if (spawnBallHail)
        {
            // Ball hail (line 2): the impact trial's own wide static lens idiom - the
            // man and the whole approach line stay inside one frame, so the film can be
            // judged uncut. Capture only: no physics row reads a camera.
            follow.frameFullBody = false;
            follow.enabled = false;
            cam.orthographicSize = ballCameraSize;
            cam.transform.position = new Vector3(0f, ballCameraY, -10f);
        }
        else if (spawnImpact)
        {
            // Impact trial: a STATIC wide lens. The man, the whole flight path and the
            // fall all stay inside one frame, so the video can be judged uncut. The
            // follow component is left on the camera but disabled.
            follow.frameFullBody = false;
            follow.enabled = false;
            cam.orthographicSize = impactCameraSize;
            cam.transform.position = new Vector3(0f, impactCameraY, -10f);
        }
        else if (appleTree != null)
        {
            // Apple trial: fixed lens on the tree. Zoomed tighter than the
            // Play `Tree` scene so the crown fills the 1080p frame.
            follow.frameFullBody = false;
            float height = appleTree.HeightMeters;
            cam.orthographicSize = height * 0.62f + 0.5f;
            cam.transform.position = new Vector3(
                appleTree.transform.position.x,
                appleTree.transform.position.y + height * 0.5f,
                -10f);
        }
        else if (human == null && plantTree != null)
        {
            // Tree trial: the same lens as Play scene `Tree` (GameProcess
            // treeOnly: size 4.6, offset 1.1). frameFullBody off — the stand
            // lens reads human head/feet sprites that are null here.
            follow.frameFullBody = false;
            follow.offset = new Vector3(0f, 1.1f, -10f);
            cam.orthographicSize = 4.6f;
            follow.Bind(plantTree);
        }
        else if (human == null && bird != null)
        {
            // B-NAT-0 film framing: the camera follows the member the numbers are about
            // — pack[0], the recorder's member 0 — not the flock centroid, so a take is
            // framed on the recorded bird. Capture only: no physics row reads a camera
            // (-nographics builds none and the physics rows are md5-checked against the
            // capture rows).
            follow.frameFullBody = false;
            follow.offset = new Vector3(0f, birdCameraOffsetY, -10f);
            cam.orthographicSize = birdCameraSize;
            follow.Bind(bird);
        }
        else if (level1StoryCam != null)
        {
            // req-246: -level1Cam takes the frame; CameraFollow is switched off so its
            // LateUpdate never fights the story camera (which owns centre and lens).
            follow.frameFullBody = false;
            follow.enabled = false;
            // req-265: steady draws the story pose every LateUpdate; the shot PNG and
            // the shot row then read the pose the window shows.
            if (level1CamSteady)
            {
                if (level1CamDraw == null)
                    level1CamDraw = cam.gameObject.AddComponent<Level1CamSteady>();
                level1CamDraw.Bind(level1StoryCam);
            }
        }
        else
        {
            follow.frameFullBody = true;
            if (human != null)
                follow.Bind(human);
            // WALK CYCLE 56: the evidence lens - a wide STATIC frame (meters), so a take can show
            // the walker crossing the old 10 m edge and continuing. Capture only: physics rows run
            // -nographics (no camera) and the take's CSV is md5-checked against the physics row.
            if (capLensSize > 0f)
            {
                follow.enabled = false;
                cam.orthographicSize = capLensSize;
                cam.transform.position = new Vector3(capLensX, capLensY, -10f);
            }
        }
        return cam;
    }

    private static void WriteFrameReadyMarker(string pngPath, int width, int height)
    {
        string folder = Path.GetDirectoryName(pngPath);
        if (string.IsNullOrEmpty(folder))
            folder = ".";
        string readyPath = Path.Combine(folder, "frame.ready.json");
        string json =
            "{\n" +
            "  \"png\": " + JsonQuote(pngPath) + ",\n" +
            "  \"width\": " + width.ToString(CultureInfo.InvariantCulture) + ",\n" +
            "  \"height\": " + height.ToString(CultureInfo.InvariantCulture) + ",\n" +
            "  \"writtenUtc\": " + JsonQuote(DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture)) + "\n" +
            "}\n";
        File.WriteAllText(readyPath, json);
    }

    private static string JsonQuote(string value)
    {
        if (value == null)
            value = "";
        return "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
    }

    private IEnumerator RunTrial()
    {
        // Skip the first step: the body is still settling in the joints.
        yield return FixedStep;

        if (spawnBallHail)
        {
            yield return RunBallTrial();
            yield break;
        }

        if (spawnImpact)
        {
            yield return RunImpactTrial();
            yield break;
        }

        if (spawnArena)
        {
            yield return RunArenaTrial();
            yield break;
        }

        // DOG ARENA (cycle 64): its own loop, its own scene, its own clock.
        if (spawnDogArena)
        {
            yield return RunDogArenaTrial();
            yield break;
        }

        if (spawnDog)
        {
            yield return RunDogTrial();
            yield break;
        }

        if (spawnBird)
        {
            yield return RunBirdTrial();
            yield break;
        }

        if (spawnFlower && !spawnTree)
        {
            yield return RunFlowerTrial();
            yield break;
        }

        if (spawnApple)
        {
            yield return RunAppleTrial();
            yield break;
        }

        if (spawnTree)
        {
            yield return RunTreeTrial();
            yield break;
        }

        recorder = new TrialRecorder(human, runInfo);
        recorder.BindWalkDriver(stepDriver);
        // TILT STAND (29.09): the per-step log of the rotating deck, when one was
        // built. Read-only instrumentation (TiltStandLog.cs).
        if (tiltStand != null)
        {
            tiltLog = new TiltStandLog(tiltStand, human, recorder, visual, tiltGetUpWanted);
            if (tiltPenWanted)
                tiltLog.penDebug = true;
            tiltLog.voidEndOn = tiltVoidEndWanted;   // ROUND 35: the practice void end
        }
        // WALK CYCLE 47: the per-plant instrument's own log (WalkPlantLog). One
        // trial is one process; it is cleared here, where the recorder is built.
        WalkPlantLog.Reset();

        float trialWallStart = Time.realtimeSinceStartup;
        float realDeadline = Time.realtimeSinceStartup + REAL_TIME_LIMIT_SECONDS;
        int lastShot = -1;

        // Step-driven trial: FixedUpdate owns the clock, the scheduled intents and
        // the sampling; this coroutine only waits for the step budget, captures
        // frames and writes the result - the dog trial's pattern (19.09), adopted
        // here because the coroutine's cadence is one resume per FRAME while Unity
        // executes a variable number of FixedUpdate calls per frame. The budget is
        // the executed step count, so the row's samples and its clock are the
        // physics the engine really ran and the rendered and the headless take of
        // one protocol are the same run (walk-cycle17 report).
        humanSteps = 0;
        humanPushed = false;
        humanDropped = false;
        humanCrouchMarked = false;
        humanCrouchReleased = false;
        humanLeanMarked = false;
        humanLeanReleased = false;
        humanStandLegMarked = false;
        humanStandLegReleased = false;
        humanStepping = true;

        while (humanStepping)
        {
            yield return FixedStep;

            if (visual)
            {
                int shotIdx = (int)(humanSteps * Time.fixedDeltaTime / shotPeriod);
                if (shotIdx != lastShot)
                {
                    lastShot = shotIdx;
                    string shotPath = Path.Combine(outputFolder, "shot_" + label + "_" + shotIdx.ToString("00") + ".png");
                    yield return CaptureVisualFrame(shotPath);
                }
            }

            if (Time.realtimeSinceStartup > realDeadline)
            {
                Debug.LogWarning("HeadlessTrial: real-time limit exceeded, run aborted.");
                humanStepping = false;
                break;
            }
        }

        float simulated = humanSteps * Time.fixedDeltaTime;
        recorder.MarkExecutedSteps(humanSteps);
        // TILT STAND (29.09): close the open step row before the summary.
        if (tiltLog != null)
            tiltLog.Finish(simulated);
        Finish(simulated, Time.realtimeSinceStartup - trialWallStart);
    }

    // ARENA-1: the sweep loop. The arena owns the bodies, this owns the clock, the
    // video frames and the real-time guard. The arena has no single human body, so
    // no TrialRecorder is created here.
    private IEnumerator RunArenaTrial()
    {
        if (arena == null)
        {
            Debug.LogError("HeadlessTrial: -arena 1 without an ArenaRunner");
            yield break;
        }

        arena.Begin(Time.fixedDeltaTime);
        if (visual)
            arena.EnsureArenaCamera(arenaCameraSize, arenaCameraOffsetX);

        float wallStart = Time.realtimeSinceStartup;
        float simulated = 0f;
        int lastShot = -1;
        int photoStage = 0;
        float[] photoTimes = { arenaPhotoTime, arenaPhotoTime + 8f, arenaPhotoTime + 16f, arenaPhotoTime + 24f };

        while (!arena.Done)
        {
            yield return FixedStep;

            simulated += Time.fixedDeltaTime;
            arena.Tick(simulated);

            if (visual)
            {
                int shotIdx = (int)(simulated / shotPeriod);
                if (shotIdx != lastShot)
                {
                    lastShot = shotIdx;
                    string shotPath = Path.Combine(outputFolder,
                        "shot_" + label + "_" + shotIdx.ToString("00") + ".png");
                    yield return CaptureVisualFrame(shotPath);
                }

                // The zoomed frames exist so the numbers on the heads are readable
                // at 1x: the wide lens is restored right after each one.
                if (photoStage < photoTimes.Length && simulated >= photoTimes[photoStage])
                {
                    arena.SetLens(arenaPhotoSize, arena.spawnX + arenaPhotoOffsetX);
                    yield return CaptureVisualFrame(Path.Combine(outputFolder,
                        "photo_" + label + "_" + photoStage.ToString("00") + ".png"));
                    arena.SetLens(arenaCameraSize, arena.spawnX + arenaCameraOffsetX);
                    photoStage++;
                }
            }

            if (Time.realtimeSinceStartup - wallStart > arenaWallLimit)
            {
                Debug.LogWarning("HeadlessTrial: arena wall limit " + arenaWallLimit + " s hit at sim "
                    + simulated.ToString("0.##", CultureInfo.InvariantCulture) + " s, run cut short.");
                break;
            }
        }

        Finish(simulated, Time.realtimeSinceStartup - wallStart);
    }

    // BALANCE-ARENA-1: the thrower beside the stand. The human is built by the stand
    // path above - same rig defaults, same components, no override - and this only
    // adds the launcher. A run with -impactCount 0 therefore still measures the quiet
    // stand, and the numbers must reproduce burn_stance.json.
    private void BuildImpactScene()
    {
        GameObject host = new GameObject("ImpactThrower");
        impactThrower = host.AddComponent<ImpactThrower>();
        impactThrower.massKg = impactMass;
        impactThrower.shapeText = impactShape;
        impactThrower.sizeMeters = impactSize;
        impactThrower.rectAspect = impactRectAspect;
        impactThrower.speedMps = impactSpeed;
        impactThrower.aimPart = impactAim;
        impactThrower.sideText = impactSide;
        impactThrower.firstThrowAt = impactAt;
        impactThrower.throwCount = impactCount;
        impactThrower.spacingSeconds = impactSpacing;
        impactThrower.seed = impactSeed;
        impactThrower.Configure(human);

        impactRecorder = new ImpactRecorder(human, impactThrower, label, outputFolder,
            runInfo != null ? runInfo.SourceFingerprint : string.Empty,
            runInfo != null ? runInfo.ManifestBuildGuid : string.Empty,
            startY, MeasuredGroundTopY(), impactWindow);

        Debug.Log("IMPACT_SCENE throws=" + impactCount.ToString(CultureInfo.InvariantCulture)
            + " shape=" + impactShape
            + " size=" + impactSize.ToString("0.###", CultureInfo.InvariantCulture)
            + " mass=" + impactMass.ToString("0.###", CultureInfo.InvariantCulture)
            + " speed=" + impactSpeed.ToString("0.###", CultureInfo.InvariantCulture)
            + " aim=" + impactAim + " side=" + impactSide
            + " at=" + impactAt.ToString("0.###", CultureInfo.InvariantCulture)
            + " seed=" + impactSeed.ToString(CultureInfo.InvariantCulture)
            + " groundTopY=" + MeasuredGroundTopY().ToString("0.####", CultureInfo.InvariantCulture));
    }

    // ─── LINE 2 — BALL HAIL (27.09, the owner's second line) ───
    // The shooter beside the stand. The human is built by the stand path above - same
    // rig defaults, same components, no override - and this only adds the launcher and
    // its restitution record (BallHail.cs / BallImpactRecorder.cs). An empty table
    // (-ballShots "") therefore still measures the quiet stand, unchanged.
    private void BuildBallScene()
    {
        GameObject host = new GameObject("BallHailShooter");
        ballShooter = host.AddComponent<BallHailShooter>();
        ballShooter.shotsText = ballShots;
        ballShooter.massKg = ballMassKg;
        ballShooter.radiusMeters = ballRadiusM;
        ballShooter.bounciness = ballBounce;
        ballShooter.friction = ballFriction;
        ballShooter.gravityScale = ballGravity;
        ballShooter.spawnDistance = ballDistance;
        ballShooter.firstShotAt = ballAt;
        ballShooter.spacingSeconds = ballSpacing;
        ballShooter.lifetimeSeconds = ballLife;
        ballShooter.segmentBounciness = ballSegBounce;
        ballShooter.segmentFriction = ballSegFriction;
        ballShooter.aimFix = ballAimFix;
        ballShooter.aimFixRange = ballAimRange;
        ballShooter.aimFixStep = ballAimStep;
        ballShooter.aimCircleCast = ballAimCast;
        ballShooter.aimSearchRange = ballAimSearch;
        ballShooter.rigDumpPath = ballRigDump.Length > 0
            ? ballRigDump
            : Path.Combine(outputFolder, label + "_rigdump.csv");
        ballShooter.GroundTopY = MeasuredGroundTopY();
        ballShooter.Configure(human);

        ballRecorder = new BallImpactRecorder(human, ballShooter, label, outputFolder,
            runInfo != null ? runInfo.SourceFingerprint : string.Empty,
            runInfo != null ? runInfo.ManifestBuildGuid : string.Empty,
            Time.fixedDeltaTime, startY);
        ballShooter.recorder = ballRecorder;

        Debug.Log("BALL_SCENE shots=" + ballShots
            + " ball_r=" + ballRadiusM.ToString("0.####", CultureInfo.InvariantCulture)
            + " ball_m=" + ballMassKg.ToString("0.####", CultureInfo.InvariantCulture)
            + " bounce=" + ballBounce.ToString("0.###", CultureInfo.InvariantCulture)
            + " friction=" + ballFriction.ToString("0.###", CultureInfo.InvariantCulture)
            + " gravityScale=" + ballGravity.ToString("0.###", CultureInfo.InvariantCulture)
            + " distance=" + ballDistance.ToString("0.###", CultureInfo.InvariantCulture)
            + " at=" + ballAt.ToString("0.###", CultureInfo.InvariantCulture)
            + " spacing=" + ballSpacing.ToString("0.###", CultureInfo.InvariantCulture)
            + " segBounce=" + ballSegBounce.ToString("0.###", CultureInfo.InvariantCulture)
            + " aimFix=" + (ballAimFix ? "1" : "0")
            + " aimRange=" + ballAimRange.ToString("0.###", CultureInfo.InvariantCulture)
            + " aimSearch=" + ballAimSearch.ToString("0.###", CultureInfo.InvariantCulture)
            + " aimCast=" + (ballAimCast ? "1" : "0")
            + " applied=" + (ballShooter.SegmentMaterialsApplied ? "1" : "0")
            + " groundTopY=" + ballShooter.GroundTopY.ToString("0.####", CultureInfo.InvariantCulture));
    }

    // The stance line, measured from the built body's own feet: the sole collider the
    // body was posed onto. Never assumed - a spawn override moves it.
    private float MeasuredGroundTopY()
    {
        if (human == null) return startY - 1.18f;
        float top = float.MaxValue;
        Transform leftFoot = human.transform.Find("LeftLegFoot");
        Transform rightFoot = human.transform.Find("RightLegFoot");
        Collider2D leftCollider = leftFoot != null ? leftFoot.GetComponent<Collider2D>() : null;
        Collider2D rightCollider = rightFoot != null ? rightFoot.GetComponent<Collider2D>() : null;
        if (leftCollider != null) top = Mathf.Min(top, leftCollider.bounds.min.y);
        if (rightCollider != null) top = Mathf.Min(top, rightCollider.bounds.min.y);
        return top == float.MaxValue ? startY - 1.18f : top;
    }

    // BALANCE-ARENA-1: the quiet stand's own loop, plus a thrower. Same FixedStep,
    // same recorder.Sample, no crouch / lean / walk / stand-leg intent of any kind:
    // the only force that reaches the body is the solver's collision with a real
    // thrown Rigidbody2D. Nothing is scripted onto the human.
    private IEnumerator RunImpactTrial()
    {
        recorder = new TrialRecorder(human, runInfo);
        recorder.BindWalkDriver(stepDriver);

        float trialWallStart = Time.realtimeSinceStartup;
        float simulated = 0f;
        float realDeadline = Time.realtimeSinceStartup + REAL_TIME_LIMIT_SECONDS;
        int lastShot = -1;

        while (simulated < duration)
        {
            yield return FixedStep;

            simulated += Time.fixedDeltaTime;

            if (impactThrower != null)
                impactThrower.Advance(simulated);
            if (impactRecorder != null)
                impactRecorder.Sample(simulated);

            recorder.Sample(simulated);

            if (visual)
            {
                int shotIdx = (int)(simulated / impactShotInterval);
                if (shotIdx != lastShot)
                {
                    lastShot = shotIdx;
                    string shotPath = Path.Combine(outputFolder,
                        "shot_" + label + "_" + shotIdx.ToString("00") + ".png");
                    yield return CaptureVisualFrame(shotPath);
                }
            }

            if (Time.realtimeSinceStartup > realDeadline)
            {
                Debug.LogWarning("HeadlessTrial: real-time limit exceeded, run aborted.");
                break;
            }
        }

        Finish(simulated, Time.realtimeSinceStartup - trialWallStart);
    }

    // ─── LINE 2 — BALL HAIL (27.09, the owner's second line) ───
    // The impact loop's own shape, with the ball shooter in the thrower's place: the
    // quiet stand's FixedStep, the same recorder.Sample, no crouch / lean / walk intent
    // of any kind. The only force that reaches the body is the solver's collision with a
    // real thrown Rigidbody2D. The hail is STEP-COUNTED (Advance takes the run's own
    // simulated time), never frame-timed.
    private IEnumerator RunBallTrial()
    {
        recorder = new TrialRecorder(human, runInfo);
        recorder.BindWalkDriver(stepDriver);

        float trialWallStart = Time.realtimeSinceStartup;
        float simulated = 0f;
        float realDeadline = Time.realtimeSinceStartup + REAL_TIME_LIMIT_SECONDS;
        int lastShot = -1;

        while (simulated < duration)
        {
            yield return FixedStep;

            simulated += Time.fixedDeltaTime;

            if (ballShooter != null)
                ballShooter.Advance(simulated);
            if (ballRecorder != null)
                ballRecorder.Sample(simulated);

            recorder.Sample(simulated);

            if (visual)
            {
                int shotIdx = (int)(simulated / shotPeriod);
                if (shotIdx != lastShot)
                {
                    lastShot = shotIdx;
                    string shotPath = Path.Combine(outputFolder,
                        "shot_" + label + "_" + shotIdx.ToString("00") + ".png");
                    yield return CaptureVisualFrame(shotPath);
                }
            }

            if (Time.realtimeSinceStartup > realDeadline)
            {
                Debug.LogWarning("HeadlessTrial: real-time limit exceeded, run aborted.");
                break;
            }
        }

        Finish(simulated, Time.realtimeSinceStartup - trialWallStart);
    }

    // One executed physics step of the dog trial. The clock, the scheduled intents
    // and the recorder sample all live here: `dogSteps` is incremented by the
    // engine's own FixedUpdate, so the trace is step-aligned and the run's length
    // is the step budget instead of whatever cadence the frame loop happened to
    // deliver (that cadence is what made `dog_art1` irreproducible).
    // DOG ARENA (cycle 64): the arena's own clock, in the SAME place the dog trial's clock lives -
    // one executed physics step of HeadlessTrial.FixedUpdate. This is deliberate: the dog trial
    // samples its recorder here (not in a `yield return FixedStep` coroutine like the human and
    // arena paths), so an arena that ticks from a coroutine samples one physics step apart from the
    // record row and no step-for-step identity would be observable. The runner's own Tick keeps its
    // bookkeeping identical; only the phase is pinned.
    private void FixedUpdate()
    {
        if (dogArenaStepping)
        {
            dogArenaSteps++;
            float arenaSim = dogArenaSteps * Time.fixedDeltaTime;
            if (dogArena != null) dogArena.Tick(arenaSim);
            if (dogArena == null || dogArena.Done || arenaSim >= dogArenaSeconds) dogArenaStepping = false;
            return;
        }

        if (dogStepping)
        {
            dogSteps++;
            float simulated = dogSteps * Time.fixedDeltaTime;

            if (!dogPushed && pushImpulse != 0f && simulated >= pushTime)
            {
                ApplyPush(simulated);
                dogPushed = true;
            }

            if (!dogLeaped && dogLeap && simulated >= dogLeapTime)
            {
                if (dog != null && dog.stance != null)
                    dog.stance.LeapAttack(dog.stance.leapTarget);
                dogLeaped = true;
            }

            if (!dogWalked && dogWalk && simulated >= dogWalkTime)
            {
                if (dog != null && dog.stance != null)
                    dog.stance.Walk();
                dogWalked = true;
            }

            if (!dogStopped && dogWalkStopTime > 0f && simulated >= dogWalkStopTime)
            {
                if (dog != null && dog.stance != null)
                    dog.stance.EndWalk();
                dogStopped = true;
            }

            dogRecorder.Sample(simulated);

            if (simulated >= duration)
                dogStepping = false;

            return;
        }

        // HUMAN STEP CLOCK (harness only, 21.09): one call per EXECUTED physics
        // step of the human trial - the clock, the scheduled intents and the
        // recorder sample all live here, exactly as the dog's block above.
        if (!humanStepping) return;

        humanSteps++;
        float humanClock = humanSteps * Time.fixedDeltaTime;
        // DELEGATION req-272: the keys edit the plan here, before any intent writer,
        // so this step's walk/crouch readers already see the new desk.
        if (keysOn) TickLevel1Keys(humanClock);

        // DEMO WALK (27.09): the engine streams the yard in 16 m chunks around
        // the walker (LevelGenerator.Update); the replica streams per step.
        if (demoWalk && human != null)
            DemoStreamChunks(human.transform.position.x, false);

        if (!humanPushed && pushImpulse != 0f && humanClock >= pushTime)
        {
            ApplyPush(humanClock);
            humanPushed = true;
        }

        UpdateCrouchIntent(humanClock, ref humanCrouchMarked, ref humanCrouchReleased);
        UpdateLeanIntent(humanClock, ref humanLeanMarked, ref humanLeanReleased);
        UpdatePunchIntent(humanClock);
        UpdateKickIntent(humanClock);
        if (marchTarget > 0.5f)
            UpdateMarchIntent(humanClock);
        else if (walkPlanKind.Length > 0 || walkTarget > 0.5f || runTarget > 0.5f)
            UpdateWalkIntent(humanClock);
        else
            UpdateStandLegIntent(humanClock, ref humanStandLegMarked, ref humanStandLegReleased);

        // TILT GET-UP (round 13, the owner's addendum 6): the pose drive's get-up
        // ladder (PoseDriveController, walk cycles 40..51: the palms plant on the
        // surface, the trunk comes up over the planted hands, the knees come under
        // the body, the rise) runs only while the man's own get-up input is pressed.
        // On the stand nobody presses keys: the man presses it HIMSELF from the
        // moment the fall criterion fires and keeps it pressed to the end of the
        // take - a successful rise leaves him standing and the deck keeps testing
        // him (fall -> rise -> fall; the rise count is per take). Inert without the
        // flag and inert before the fall, so every row without -tiltGetUp 1 keeps
        // its bytes.
        // CYCLE 98 (the flat ground arm): the deck requirement is DROPPED. On flat
        // ground this block was dead code (tiltWanted false), so a supine landing
        // lay perfectly still. Now -tiltGetUp 1 alone arms the self-press wherever
        // the man goes down; every tilt row reads the same condition as before, so
        // the tilt family and every flapless row keep their bytes.
        if (tiltGetUpWanted && intent != null)
        {
            // CYCLE 98 (the owner's addendum 7, 01.10): the press is FLAT-CAPABLE
            // now (the deck requirement at the condition is gone); this log is the
            // rise family's own evidence and the DLL carry token.
            if (recorder != null && recorder.PhysicalFallTime >= 0f && !humanGetUpKey)
                Debug.Log("GETUP-FLAT press t=" + humanClock);
            if (recorder != null && recorder.PhysicalFallTime >= 0f)
                humanGetUpKey = true;
            intent.moveX = humanGetUpKey ? 1f : 0f;
            if (tiltGetUpHoldWanted && humanGetUpKey && tiltStand != null)
                tiltStand.rampFrozen = true;
        }

        // DROP TEST (line 3, 27.09): the release fires ONCE, on the first step whose
        // clock reaches -dropTime, BEFORE the recorder samples - so the row at that
        // step already shows the lifted rig and the release time is the clock itself.
        if (!humanDropped && dropHeight > 0f && humanClock >= dropTime)
        {
            ReleaseDrop();
            humanDropped = true;
        }

        recorder.Sample(humanClock);

        // LEVEL 1 (req-217 step 2): the flock's answer to the approach, same step.
        if (level1Crows != null)
            SampleLevel1Crows(humanClock);

        // req-246: the storyteller camera state on the same step (reads only).
        if (level1StoryCam != null)
            TickLevel1Cam(humanClock);

        // TILT STAND (29.09): per-step bookkeeping right after the sample; the
        // take ends a short settle after the fall, so the film keeps the fall.
        if (tiltLog != null)
        {
            tiltLog.Sample(humanClock);
            if (tiltLog.WantsEnd)
                humanStepping = false;
        }

        if (humanClock >= duration)
            humanStepping = false;
    }

    private IEnumerator RunDogTrial()
    {
        dogRecorder = new DogTrialRecorder(dog, runInfo);
        // CYCLE 138 (req-224): the true contact-point slide sensor, default off.
        // The marked dots are picture only: visual capture with the switch on.
        dogRecorder.slipTrueOn = GetFloatArg("-dogSlipTrue", 0f) > 0.5f;
        dogRecorder.markPoints = visual && dogRecorder.slipTrueOn;

        float trialWallStart = Time.realtimeSinceStartup;
        float realDeadline = Time.realtimeSinceStartup + REAL_TIME_LIMIT_SECONDS;
        int lastShot = -1;

        // Step-driven trial: FixedUpdate owns the clock, the scheduled intents and
        // the sampling; this coroutine only waits for the step budget, captures
        // frames and writes the result. The budget is exact - duration /
        // fixedDeltaTime steps - so a run can no longer end 5 ms late here and
        // 35 ms late there, which is the difference that broke the detector.
        dogSteps = 0;
        dogPushed = false;
        dogLeaped = false;
        dogWalked = false;
        dogStopped = false;
        dogStepping = true;

        while (dogStepping)
        {
            yield return FixedStep;

            // Same frame capture as the human and bird trials: the dog had none, so
            // there were no dog videos to look at. Yury asked for dog video reports.
            if (visual)
            {
                int shotIdx = (int)(dogSteps * Time.fixedDeltaTime / shotPeriod);
                if (shotIdx != lastShot)
                {
                    lastShot = shotIdx;
                    string shotPath = Path.Combine(outputFolder, "shot_" + label + "_" + shotIdx.ToString("00") + ".png");
                    yield return CaptureVisualFrame(shotPath);
                }
            }

            if (Time.realtimeSinceStartup > realDeadline)
            {
                Debug.LogWarning("HeadlessTrial: real-time limit exceeded, run aborted.");
                dogStepping = false;
                break;
            }
        }

        float simulated = dogSteps * Time.fixedDeltaTime;
        dogRecorder.MarkExecutedSteps(dogSteps);
        Finish(simulated, Time.realtimeSinceStartup - trialWallStart);
    }

    private IEnumerator RunBirdTrial()
    {
        birdRecorder = new BirdTrialRecorder(bird, runInfo, trialFlockDrive, trialBirds, plantTree);
        if (plantTree != null)
            plantRecorder = new PlantTrialRecorder(plantTree, runInfo);

        float trialWallStart = Time.realtimeSinceStartup;
        float simulated = 0f;
        float realDeadline = Time.realtimeSinceStartup + REAL_TIME_LIMIT_SECONDS;
        bool pushed = false;
        bool flapStopped = false;
        int lastShot = -1;

        while (simulated < duration)
        {
            yield return FixedStep;
            simulated += Time.fixedDeltaTime;

            if (!flapStopped && stopFlapTime > 0.01f && simulated >= stopFlapTime
                && bird != null && bird.controller != null)
            {
                // Without a flap it does not hover: it glides and lands. Stone drop — stand.
                bird.controller.mode = stopFlapMode == "stand" ? BirdMode.Stand : BirdMode.Glide;
                bird.controller.takeoffDelay = 0f;
                flapStopped = true;
            }

            if (!pushed && pushImpulse != 0f && simulated >= pushTime)
            {
                ApplyPush(simulated);
                pushed = true;
            }

            birdRecorder.Sample(simulated);
            if (plantRecorder != null)
                plantRecorder.Sample(simulated);

            if (visual)
            {
                int shotIdx = (int)(simulated / shotPeriod);
                if (shotIdx != lastShot)
                {
                    lastShot = shotIdx;
                    string shotPath = Path.Combine(outputFolder, "shot_" + label + "_" + shotIdx.ToString("00") + ".png");
                    yield return CaptureVisualFrame(shotPath);
                }
            }

            if (Time.realtimeSinceStartup > realDeadline)
            {
                Debug.LogWarning("HeadlessTrial: real-time limit exceeded, run aborted.");
                break;
            }
        }

        Finish(simulated, Time.realtimeSinceStartup - trialWallStart);
    }

    // Apply the impulse to the torso: that is a shove at the shoulder, not the foot.
    private void ApplyPush(float time)
    {
        if (torsoBody == null)
        {
            Debug.LogWarning("HeadlessTrial: torso not found, push not applied.");
            return;
        }

        torsoBody.AddForce(new Vector2(pushImpulse, 0f), ForceMode2D.Impulse);
        if (recorder != null)
            recorder.MarkPush(time, pushImpulse);
        if (dogRecorder != null)
            dogRecorder.MarkPush(time, pushImpulse);
    }

    // ─── DROP TEST (owner's line 3, 27.09) ───
    // The rig is OFFSET, not re-spawned: every rigid body under the human root moves
    // up by the same amount, so every hinge keeps the anchor relation it had at
    // release and the pose arrives at the floor unchanged. All velocities are zeroed
    // first: the release is the START of a fall, not a throw. Nothing tells the
    // balance controller what happened - it meets the floor through its own contacts,
    // exactly as it meets the ground after a step. No torque, target or state of any
    // controller is written here; this method only moves bodies.
    private void ReleaseDrop()
    {
        if (human == null)
        {
            Debug.LogWarning("HeadlessTrial: -drop given but no human rig exists.");
            return;
        }
        Rigidbody2D[] rig = human.GetComponentsInChildren<Rigidbody2D>(true);
        for (int i = 0; i < rig.Length; i++)
        {
            rig[i].WakeUp();
            rig[i].linearVelocity = Vector2.zero;
            rig[i].angularVelocity = 0f;
        }
        for (int i = 0; i < rig.Length; i++)
            rig[i].position = rig[i].position + new Vector2(0f, dropHeight);
        for (int i = 0; i < rig.Length; i++)
            rig[i].WakeUp();
        dropBodyCount = rig.Length;
        dropReleaseStep = humanSteps;
        dropReleaseTime = humanSteps * Time.fixedDeltaTime;
        Debug.Log("DROP_RELEASE label=" + label
            + " h=" + dropHeight.ToString("0.###", CultureInfo.InvariantCulture)
            + " t=" + dropReleaseTime.ToString("0.0000", CultureInfo.InvariantCulture)
            + " step=" + dropReleaseStep
            + " bodies=" + dropBodyCount);
    }

    // Rewrite each hold window every step: if PlayerInputSource
    // suddenly wakes, it cannot erase the script in one frame.
    //
    // A4 scripted squat-and-rise rides the same channel: -squatRise 1 schedules
    // intent.crouch = 1 for the window [squatRiseTime, squatRiseTime +
    // squatRiseHold) and 0 outside it. The crouch pose itself (hips via the
    // crouch pelvis tilt term, knees via crouchKneeFlex) and both rates are the
    // controller's own - this input writes a TARGET and nothing else. The
    // extension is therefore the existing release ramp: crouchLevel falls at
    // crouchReleaseRatePerSecond (0.9/s), so a full 1 -> 0 takes ~1.1 s.
    private void UpdateCrouchIntent(float time, ref bool marked, ref bool released)
    {
        if (intent == null) return;

        bool holding = crouchTarget > 0f && time >= crouchTime && time < crouchTime + crouchHold;
        float level = holding ? crouchTarget : 0f;
        bool squatting = squatRise > 0f
            && time >= squatRiseTime && time < squatRiseTime + squatRiseHold;
        if (squatting) level = squatRise;
        // SQUAT TASK (cycle 84): the same channel, with a COUNT. Inside the task window
        // the trial writes crouch = 1 and squatReps = N; the machine in BalanceController
        // sequences the reps (its own bottom/stand gates) and stops after the Nth, so
        // nothing here knows what a squat is - the trial asks, the controller poses.
        // CYCLE 96: with a plan the s/r desks are the squat window; the machine's own
        // gates still sequence the rep - the desk only says WHEN the machine is asked.
        char planDeskNow = walkPlanKind.Length > 0 ? WalkPlanKindAt(time) : ' ';
        bool task = walkPlanKind.Length > 0
            ? (planDeskNow == 's' || planDeskNow == 'r')
            : squatReps > 0 && time >= squatTaskTime && time < squatTaskTime + squatTaskHold;
        // WALK CYCLE 94: a take that squats keeps the stand's flat-sole toe hold out
        // of the WHOLE run (see MotionIntent.toeHoldBlock) - the hold's posture at
        // t = 0 moves the descent and the landed squat cannot absorb it.
        intent.toeHoldBlock = (squatReps > 0 || walkPlanKind.Length > 0) ? 1 : 0;
        if (task)
        {
            level = 1f;
            intent.squatReps = walkPlanKind.Length > 0 ? walkPlanReps : squatReps;
            // The two doses travel with the arm, so a take that arms nothing writes
            // neither of them and every earlier path is untouched.
            intent.squatPace = squatPace;
            intent.squatBottomHold = squatBottomHold;
            intent.squatReleaseScale = squatReleaseScale;
            intent.squatReleaseLoad = squatReleaseLoad;
            intent.squatReleaseSink = squatReleaseSink;
            intent.squatReleaseFloor = squatReleaseFloor;
            intent.squatBottomGate = squatBottomGate;
            intent.squatBottomSettle = squatBottomSettle;
            intent.squatBottomMps = squatBottomMps;
            intent.squatRiseCom = squatRiseCom;
            intent.squatRiseComScale = squatRiseComScale;
            intent.squatRiseComMax = squatRiseComMax;
        }
        else if (intent.squatReps != 0)
        {
            intent.squatReps = 0;
        }
        // CYCLE 90: the arm raise. Its own motion, not part of the squat task: the LEVEL
        // rises when this trial's own clock passes armRaiseTime (that rising edge is the
        // gate the controller latches), and the schedule rides along. 0 on every other
        // take, so no earlier path reads anything new.
        intent.armRaise = armRaise > 0 && time >= armRaiseTime ? 1 : 0;
        intent.armRaiseShoulder = armRaiseShoulderDeg;
        intent.armRaiseElbow = armRaiseElbowDeg;
        intent.armRaiseBend = armRaiseBendDeg;
        intent.armRaiseHold = armRaiseHold;
        intent.armRaiseReps = armRaiseReps;
        intent.armRaisePeriod = armRaisePeriod;
        // The tilt ride's crouch dose rides the same field (round 6): the trial owns the
        // write; a take without a deck passes null and the line is the former arithmetic.
        intent.crouch = tiltRide != null ? Mathf.Clamp01(level + tiltRide.CrouchDose) : level;
        holding = holding || squatting || task;

        if (holding && !marked)
        {
            // level, not crouchTarget: on the -squatRise schedule the target is
            // 1 and crouchTarget is 0, and the JSON's requestedCrouch must name
            // the level that was actually asked for.
            recorder.MarkCrouch(time, level);
            marked = true;
        }
        else if (marked && !released && !holding)
        {
            recorder.MarkCrouchRelease(time);
            released = true;
        }
    }

    // Arm strike via MotionIntent: the same channel the editor's F key drives
    // (PlayerInputSource). On while the window is open; ArmDrive runs the stroke
    // itself - the trial only writes the request, exactly as the key does.
    // Punch cycle p3: `-punchPause <s>` splits the hold window into TWO bursts
    // of the same length, `punchPause` seconds apart (the recovery rung: does
    // the stand recover between bursts the way it recovers between strokes?).
    // 0 = the landed single window, bit for bit.
    // Punch cycle p4: `-punchBursts "9,9,9"` names the burst lengths instead
    // (the third burst / other burst lengths at the holding pause); with the
    // list absent every expression below is the landed one.
    private void UpdatePunchIntent(float time)
    {
        if (intent == null) return;

        bool holding = false;
        if (punchTarget > 0f)
        {
            if (punchBurstLens != null && punchBurstLens.Length > 0)
            {
                float start = punchTime;
                for (int i = 0; i < punchBurstLens.Length; i++)
                {
                    float len = punchBurstLens[i];
                    if (time >= start && time < start + len) { holding = true; break; }
                    start += len + punchPause;
                }
            }
            else if (punchPause > 0.0001f)
            {
                float second = punchTime + punchHold + punchPause;
                holding = (time >= punchTime && time < punchTime + punchHold)
                    || (time >= second && time < second + punchHold);
            }
            else
            {
                holding = time >= punchTime && time < punchTime + punchHold;
            }
        }
        intent.punch = holding ? 1f : 0f;
    }

    // Punch cycle p4: `-punchBursts "9,9,9"` -> the three window lengths in
    // seconds. Anything unparsable or non-positive is dropped; an empty or
    // all-garbage list returns null = the landed shapes.
    private static float[] ParsePunchBurstLens(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return null;
        string[] parts = raw.Split(new[] { ',', ';', ' ' }, System.StringSplitOptions.RemoveEmptyEntries);
        var lens = new System.Collections.Generic.List<float>();
        foreach (string part in parts)
        {
            float v;
            if (float.TryParse(part, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out v) && v > 0.0001f)
                lens.Add(v);
        }
        return lens.Count > 0 ? lens.ToArray() : null;
    }

    // Leg strike via MotionIntent: the same channel the editor's G key drives
    // (PlayerInputSource). On while the window is open; LegStrikeDrive runs the
    // stroke itself - the trial only writes the request, exactly as the key does.
    private void UpdateKickIntent(float time)
    {
        if (intent == null) return;

        bool holding = kickTarget > 0f && time >= kickTime && time < kickTime + kickHold;
        intent.kick = holding ? 1f : 0f;
    }

    // Lean via MotionIntent: +1 forward, −1 back (same as the Play arrows).
    private void UpdateLeanIntent(float time, ref bool marked, ref bool released)
    {
        if (intent == null) return;

        bool holding = Mathf.Abs(leanTarget) > 0.01f
            && time >= leanTime && time < leanTime + leanHold;
        // The tilt ride's lean dose rides the same field (round 6, Assets/TiltLean.cs).
        float leanWrite = holding ? leanTarget : 0f;
        intent.lean = tiltRide != null
            ? Mathf.Clamp(leanWrite + tiltRide.LeanDose, -1f, 1f)
            : leanWrite;

        if (holding && !marked)
            marked = true;
        else if (marked && !released && !holding)
            released = true;
    }

    // Timed standLeg swap: both supports until walkTime, then ±1 every walkStance.
    // -run 1 in the runTime…runHold window sets intent.run (shorter Stance).
    // FLAT SEQUENCE (cycle 96): the control plan (see ParseWalkPlan). Desks partition
    // [0, walkPlanTotal); past the last desk the take stands quietly - the tail after
    // the plan is a plain idle stand, never a resumed walk window.
    private char WalkPlanKindAt(float time)
    {
        for (int i = 0; i < walkPlanKind.Length; i++)
            if (time < walkPlanEnd[i]) return walkPlanKind[i];
        return 'i';
    }

    private float WalkPlanStartAt(float time)
    {
        for (int i = 0; i < walkPlanKind.Length; i++)
            if (time < walkPlanEnd[i]) return walkPlanStart[i];
        return walkPlanTotal;
    }

    // CYCLE 97: index of the desk that owns `time`; -1 = the quiet tail past the plan.
    private int WalkPlanDeskIndexAt(float time)
    {
        for (int i = 0; i < walkPlanKind.Length; i++)
            if (time < walkPlanEnd[i]) return i;
        return -1;
    }

    // CYCLE 97: the stop brake window. True while a finished w desk's momentum is
    // being shed inside the idle desk (or the tail) that follows it. Only an idle
    // desk brakes: a walk handed straight to a squat desk keeps the old hard stop,
    // and the brake never runs past its own desk's end.
    private bool WalkPlanBraking(float time)
    {
        if (walkPlanBrake <= 0f || walkPlanKind.Length == 0) return false;
        int i = WalkPlanDeskIndexAt(time);
        char prev; float deskStart; float deskEnd;
        if (i < 0)
        {
            prev = walkPlanKind[walkPlanKind.Length - 1];
            deskStart = walkPlanTotal;
            deskEnd = float.MaxValue;
        }
        else
        {
            if (walkPlanKind[i] != 'i' || i == 0) return false;
            prev = walkPlanKind[i - 1];
            deskStart = walkPlanStart[i];
            deskEnd = walkPlanEnd[i];
        }
        if (prev != 'w') return false;
        return time < Mathf.Min(deskStart + walkPlanBrake, deskEnd);
    }

    // The brake's own progress 0..1 (shared by the seats it drives).
    // CYCLE 124 fit rule: a brake never cuts mid-ramp - when the desk ends before the
    // brake window does, the ramp is re-paced to reach 1 at the desk's own end (the
    // measured fix for the door's rocking stand: the mid-ramp cut at moveX 0.54 was
    // the defect; the ramped-to-floor drop quiets the halt and the squat desk folds).
    private float WalkPlanBrakeU(float time)
    {
        int i = WalkPlanDeskIndexAt(time);
        float deskStart = i < 0 ? walkPlanTotal : walkPlanStart[i];
        float span = walkPlanBrake;
        if (walkPlanBrakeFit > 0f && i >= 0)
        {
            float deskLen = walkPlanEnd[i] - deskStart;
            if (deskLen < span) span = Mathf.Max(deskLen, 1e-4f);
        }
        return Mathf.Clamp01((time - deskStart) / span);
    }

    // The brake's moveX ramp: 1 at the boundary -> walkPlanBrakeFloor at its end
    // (the floor keeps the sign alive for the drivers' |moveX| gates).
    private float WalkPlanBrakeMoveX(float time)
    {
        return Mathf.Lerp(1f, walkPlanBrakeFloor, WalkPlanBrakeU(time));
    }

    // Syntax: comma-separated desks, each <letter><seconds> - w walk, i idle stand,
    // s squat descend/hold desk, r squat rise desk. The s*r stretch arms the squat
    // task machine (BalanceController's SquatStateMachine); its own gates sequence
    // descend/bottom/rise, so the halves carry only the timing and the desk must
    // cover the cycle. A bad token logs an error and leaves the plan OFF, so a typo
    // can never fudge a row.
    private void ParseWalkPlan()
    {
        walkPlanKind = new char[0];
        string raw = (walkPlanRaw == null ? "" : walkPlanRaw).Trim();
        if (raw.Length == 0) return;
        string[] parts = raw.Split(',');
        List<char> kinds = new List<char>(parts.Length);
        List<float> starts = new List<float>(parts.Length);
        List<float> ends = new List<float>(parts.Length);
        float t = 0f;
        for (int i = 0; i < parts.Length; i++)
        {
            string desk = parts[i].Trim();
            char k = desk.Length > 0 ? char.ToLowerInvariant(desk[0]) : '?';
            float d = 0f;
            bool ok = desk.Length >= 2
                && (k == 'w' || k == 'i' || k == 's' || k == 'r')
                && float.TryParse(desk.Substring(1), NumberStyles.Float,
                       CultureInfo.InvariantCulture, out d)
                && d > 0f;
            if (!ok)
            {
                Debug.LogError("[walkPlan] bad desk '" + desk + "' - plan OFF");
                walkPlanKind = new char[0];
                return;
            }
            kinds.Add(k);
            starts.Add(t);
            t += d;
            ends.Add(t);
        }
        walkPlanKind = kinds.ToArray();
        walkPlanStart = starts.ToArray();
        walkPlanEnd = ends.ToArray();
        walkPlanTotal = t;
        walkPlanReps = Mathf.Max(1, squatReps);
        Debug.Log("[walkPlan] active: " + raw + " | desks=" + walkPlanKind.Length
            + " total=" + walkPlanTotal.ToString("0.###", CultureInfo.InvariantCulture)
            + "s reps=" + walkPlanReps);
    }

    // ── DELEGATION req-272: keys -> plan desks ────────────────────────────────
    // With no -walkPlan the live plan is seeded at the FIRST key (CutPlanAt) as one
    // open idle desk, so from that step every plan reader (walk window, squat window,
    // toeHoldBlock) behaves as on a plan take; before it the take is the no-plan take.
    // With -walkPlan that plan is the intro and the first key cuts it.
    private void SeedLivePlan()
    {
        if (walkPlanKind.Length > 0) return;
        walkPlanKind = new char[] { 'i' };
        walkPlanStart = new float[] { 0f };
        walkPlanEnd = new float[] { float.MaxValue };
        walkPlanTotal = float.MaxValue;
        walkPlanReps = Mathf.Max(1, squatReps);
        Debug.Log("[keys] live plan seeded: i [0,inf)");
    }

    // The first key takes over: the desk that owns t ends at t and every later desk
    // is dropped. A t past the intro's tail keeps a closing idle desk so the brake's
    // previous-desk rule still sees the walk it followed.
    private void CutPlanAt(float t)
    {
        int n = walkPlanKind.Length;
        if (n == 0) { SeedLivePlan(); n = walkPlanKind.Length; }
        if (t >= walkPlanTotal && walkPlanTotal < float.MaxValue * 0.5f)
        {
            Array.Resize(ref walkPlanKind, n + 1);
            Array.Resize(ref walkPlanStart, n + 1);
            Array.Resize(ref walkPlanEnd, n + 1);
            walkPlanKind[n] = 'i';
            walkPlanStart[n] = walkPlanTotal;
            walkPlanEnd[n] = t;
            n++;
        }
        int idx = n - 1;
        for (int i = 0; i < n; i++)
            if (t < walkPlanEnd[i]) { idx = i; break; }
        if (idx < 0) idx = 0;
        if (idx + 1 < n)
        {
            Array.Resize(ref walkPlanKind, idx + 1);
            Array.Resize(ref walkPlanStart, idx + 1);
            Array.Resize(ref walkPlanEnd, idx + 1);
        }
        walkPlanEnd[idx] = Mathf.Max(walkPlanStart[idx], t);
        Debug.Log("[keys] takeover cut at t=" + t.ToString("0.###", CultureInfo.InvariantCulture)
            + " desks=" + walkPlanKind.Length);
    }

    // The open desk (end = float.MaxValue) is the one a key edits. Two edges on one
    // step replace the open desk's kind instead of leaving a zero-length desk.
    private bool PushDesk(char k, float t, string src, int mask)
    {
        int n = walkPlanKind.Length;
        bool open = n > 0 && walkPlanEnd[n - 1] >= float.MaxValue * 0.5f;
        if (open && walkPlanKind[n - 1] == k)
        {
            TrialRecorder.KeyDesk = DeskCode(k);
            TrialRecorder.KeyMask = mask;
            return false;
        }
        if (open)
        {
            walkPlanEnd[n - 1] = Mathf.Max(walkPlanStart[n - 1], t);
            if (walkPlanEnd[n - 1] <= walkPlanStart[n - 1] + 1e-5f)
            {
                walkPlanEnd[n - 1] = float.MaxValue;
                walkPlanKind[n - 1] = k;
                WriteKeyRow(t, k, src, mask);
                return true;
            }
        }
        Array.Resize(ref walkPlanKind, n + 1);
        Array.Resize(ref walkPlanStart, n + 1);
        Array.Resize(ref walkPlanEnd, n + 1);
        walkPlanKind[n] = k;
        walkPlanStart[n] = t;
        walkPlanEnd[n] = float.MaxValue;
        WriteKeyRow(t, k, src, mask);
        return true;
    }

    private static int DeskCode(char k)
    {
        if (k == 'i') return 1;
        if (k == 'w') return 2;
        if (k == 's' || k == 'r') return 3;
        return 0;
    }

    // Bits: 1 D/right, 2 A/left, 4 S/down, 8 W/up, 16 space (logged, no action v1).
    private int ReadKeyMask(float t)
    {
        if (keyMaskScript.Count > 0)
        {
            for (int i = 0; i < keyMaskScript.Count; i++)
                if (t >= keyMaskScript[i].t0 && t < keyMaskScript[i].t1)
                    return keyMaskScript[i].mask;
            return 0;
        }
        var kb = Keyboard.current;
        if (kb == null) return 0;
        int m = 0;
        if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) m |= 1;
        if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) m |= 2;
        if (kb.sKey.isPressed || kb.downArrowKey.isPressed) m |= 4;
        if (kb.wKey.isPressed || kb.upArrowKey.isPressed) m |= 8;
        if (kb.spaceKey.isPressed) m |= 16;
        return m;
    }

    private void TickLevel1Keys(float t)
    {
        if (keyBalance == null && human != null)
        {
            keyBalance = human.GetComponent<BalanceController>();
            keyBody = human.GetComponent<Rigidbody2D>();
        }
        if (keyReplayRows.Count > 0) { ApplyKeyReplay(t); return; }
        int mask = ReadKeyMask(t);
        if (!keysTakenOver)
        {
            if (mask == 0) return;                 // no key: the plan is untouched
            keysTakenOver = true;
            keyTakeoverT = t;
            CutPlanAt(t);
        }
        string src;
        char desk = DeskFromMask(mask, t, out src);
        PushDesk(desk, t, src, mask);
        TrialRecorder.KeyDesk = DeskCode(desk);
        TrialRecorder.KeyMask = mask;
        keyMaskPrev = mask;
    }

    // The rule table of keys-design.md, in its order. Each rule carries its src.
    private char DeskFromMask(int mask, float t, out string src)
    {
        src = "k";
        bool sKey = (mask & 4) != 0;
        bool dKey = (mask & 1) != 0;
        bool aKey = (mask & 2) != 0;
        bool wKey = (mask & 8) != 0;
        bool sEdge = sKey && (keyMaskPrev & 4) == 0;
        // A fresh walk press only: a held key must not undo the stop the S edge made,
        // or the queued squat would never see a quiet body (E5 without the brake).
        bool dEdge = dKey && (keyMaskPrev & 1) == 0;
        bool wEdge = wKey && (keyMaskPrev & 8) == 0;
        // 1: a fall owns the body, and so does the whole get-up: locomotionState flicks
        // through Idle between the rise phases, so a held walk key made i/w churn 5 ms
        // apart (E9). The desk stays i until the body is quiet for 0.3 s after the rise.
        if (keyBalance != null)
        {
            BalanceController.LocomotionState ls = keyBalance.locomotionState;
            if (ls == BalanceController.LocomotionState.Falling || ls == BalanceController.LocomotionState.Fallen
                || ls == BalanceController.LocomotionState.GettingUp)
            {
                if (keySquatLatch || keySquatQueued) keyFallenRows++;
                keySquatLatch = false;
                keySquatQueued = false;
                if (!keyRecover) keyRecover = true;
                keyRecoverT = 0f;
                src = "f";
                return 'i';
            }
            if (keyRecover)
            {
                bool still = (keyBody == null || Mathf.Abs(keyBody.linearVelocity.x) < 0.2f)
                    && (stepDriver == null || !stepDriver.walkActive);
                keyRecoverT = still ? keyRecoverT + Time.fixedDeltaTime : 0f;
                if (keyRecoverT < 0.3f) { src = "f"; return 'i'; }
                keyRecover = false;
            }
        }
        // 2: the squat latch - one rep per press
        if (keySquatLatch)
        {
            int sc = keyBalance != null ? keyBalance.SquatStateCode : 0;
            bool done = sc == 5;                                   // SquatState.Done
            bool idle = sc == 0 && t - keySquatLatchT >= 0.5f;     // Idle after the rep
            bool cap = t - keySquatLatchT >= 12f;
            if (done || idle || cap) keySquatLatch = false;
            else { src = "s"; return 's'; }
        }
        // 3/4: the S rising edge
        if (sEdge)
        {
            bool walking = stepDriver != null
                && (stepDriver.walkActive || (poseDrive != null && poseDrive.ClReleaseRampPressEnds));
            if (walking && !keySquatBrakeWalk)
            {
                keySquatQueued = true;
                keyQueuedDesks++;
                src = "q";
                return 'i';
            }
            keySquatLatch = true;
            keySquatLatchT = t;
            keySquatDesks++;
            src = "s";
            return 's';
        }
        // 5: the queued squat starts when the body is quiet
        if (keySquatQueued && QuietFor())
        {
            keySquatQueued = false;
            keySquatLatch = true;
            keySquatLatchT = t;
            keySquatDesks++;
            src = "q";
            return 's';
        }
        // 6: a fresh walk press cancels a queued squat and walks
        if ((dEdge || wEdge) && !aKey)
        {
            keySquatQueued = false;
            src = "k";
            return 'w';
        }
        // 6b: a held walk key walks, unless a squat is still queued - then the stop
        // must finish first, or the quiet test never sees the body (E5, no brake)
        if ((dKey || wKey) && !aKey && !keySquatQueued)
        {
            src = "k";
            return 'w';
        }
        // 7: no key, A alone, A+D
        src = "k";
        return 'i';
    }

    // Quiet = no walk, both soles down and the body's own horizontal speed small,
    // held for 0.3 s. Accumulated per step, only asked while a squat is queued.
    private bool QuietFor()
    {
        bool quiet = (stepDriver == null || !stepDriver.walkActive)
            && (poseDrive == null || poseDrive.ReflexBothSolesDown)
            && (keyBody == null || Mathf.Abs(keyBody.linearVelocity.x) < 0.15f);
        if (!quiet) { keyQuietT = 0f; return false; }
        keyQuietT += Time.fixedDeltaTime;
        return keyQuietT >= 0.3f;
    }

    private void OpenKeyLog()
    {
        try
        {
            string p = Path.Combine(outputFolder, label + "_keys.csv");
            keyLog = new StreamWriter(p, false, new UTF8Encoding(false));
            keyLog.AutoFlush = true;
            keyLog.WriteLine("# level1Keys v1 plan=\"" + walkPlanRaw + "\" fixedDelta="
                + Time.fixedDeltaTime.ToString("R", CultureInfo.InvariantCulture)
                + " seed=" + Mathf.RoundToInt(GetFloatArg("-level1Seed", 1f)) + " fp=" + (runInfo != null ? runInfo.SourceFingerprint : ""));
            keyLog.WriteLine("step,t,desk,src,mask");
        }
        catch (Exception e)
        {
            Debug.LogError("[keys] log open failed: " + e.Message);
            keyLog = null;
        }
    }

    private void WriteKeyRow(float t, char desk, string src, int mask)
    {
        keyEdges++;
        if (keyLog != null)
            keyLog.WriteLine(humanSteps.ToString(CultureInfo.InvariantCulture) + ","
                + t.ToString("R", CultureInfo.InvariantCulture) + "," + desk + "," + src + ","
                + mask.ToString(CultureInfo.InvariantCulture));
        Debug.Log("[keys] t=" + t.ToString("0.###", CultureInfo.InvariantCulture) + " desk=" + desk
            + " src=" + src + " mask=" + mask);
    }

    private void LoadKeyReplay()
    {
        if (keyReplayPath.Length == 0) return;
        try
        {
            if (!File.Exists(keyReplayPath)) { Debug.LogError("[keys] replay file missing: " + keyReplayPath); return; }
            string[] lines = File.ReadAllLines(keyReplayPath);
            for (int i = 0; i < lines.Length; i++)
            {
                string ln = lines[i].Trim();
                if (ln.Length == 0 || ln[0] == '#' || ln.StartsWith("step,")) continue;
                string[] f = ln.Split(',');
                if (f.Length < 3) continue;
                KeyRow r = new KeyRow();
                int.TryParse(f[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out r.step);
                float.TryParse(f[1], NumberStyles.Float, CultureInfo.InvariantCulture, out r.t);
                r.desk = f[2].Length > 0 ? f[2][0] : 'i';
                r.src = f.Length > 3 ? f[3] : "k";
                if (f.Length > 4) int.TryParse(f[4], NumberStyles.Integer, CultureInfo.InvariantCulture, out r.mask);
                keyReplayRows.Add(r);
            }
            Debug.Log("[keys] replay rows=" + keyReplayRows.Count + " file=" + keyReplayPath);
        }
        catch (Exception e) { Debug.LogError("[keys] replay read failed: " + e.Message); }
    }

    private void ApplyKeyReplay(float t)
    {
        while (keyReplayIdx < keyReplayRows.Count)
        {
            KeyRow r = keyReplayRows[keyReplayIdx];
            bool due = r.step > 0 ? humanSteps >= r.step : t >= r.t - 1e-5f;
            if (!due) break;
            keyReplayIdx++;
            if (!keysTakenOver)
            {
                keysTakenOver = true;
                keyTakeoverT = r.t;
                CutPlanAt(r.t);
            }
            PushDesk(r.desk, r.t, r.src, r.mask);
            TrialRecorder.KeyDesk = DeskCode(r.desk);
            TrialRecorder.KeyMask = r.mask;
        }
    }

    private void LoadKeyMaskScript()
    {
        if (keyMaskScriptPath.Length == 0) return;
        try
        {
            if (!File.Exists(keyMaskScriptPath)) { Debug.LogError("[keys] mask script missing: " + keyMaskScriptPath); return; }
            string[] lines = File.ReadAllLines(keyMaskScriptPath);
            for (int i = 0; i < lines.Length; i++)
            {
                string ln = lines[i].Trim();
                if (ln.Length == 0 || ln[0] == '#') continue;
                string[] f = ln.Split(',');
                if (f.Length < 3) continue;
                MaskRow m = new MaskRow();
                float.TryParse(f[0], NumberStyles.Float, CultureInfo.InvariantCulture, out m.t0);
                float.TryParse(f[1], NumberStyles.Float, CultureInfo.InvariantCulture, out m.t1);
                int.TryParse(f[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out m.mask);
                keyMaskScript.Add(m);
            }
            Debug.Log("[keys] mask script rows=" + keyMaskScript.Count + " file=" + keyMaskScriptPath);
        }
        catch (Exception e) { Debug.LogError("[keys] mask script read failed: " + e.Message); }
    }

    private void CloseKeyLog()
    {
        WriteKeyRow(0f, 'e', "end", 0);
        if (keyLog != null)
        {
            keyLog.Flush();
            keyLog.Close();
            keyLog = null;
        }
        Debug.Log("TRIAL_KEYS_CSV " + Path.Combine(outputFolder, label + "_keys.csv"));
        Debug.Log("[keys] summary edges=" + keyEdges + " takeover="
            + keyTakeoverT.ToString("0.###", CultureInfo.InvariantCulture)
            + " squats=" + keySquatDesks + " queued=" + keyQueuedDesks + " fallenRows=" + keyFallenRows);
        TrialRecorder.KeyColumns = false;
    }

    private void UpdateWalkIntent(float time)
    {
        if (intent == null || stepDriver == null) return;

        // CYCLE 96: with a plan the desks own the walk window; the single window is
        // then the no-plan path, bit for bit as before.
        bool walkWindow = walkPlanKind.Length > 0
            ? WalkPlanKindAt(time) == 'w'
            : walkTarget > 0.5f && time >= walkTime && time < walkTime + walkDuration;
        // req-117/121 phase release: the held key-up; `other` from THIS evaluation's scheduled windows
        // (not the intent's last-written fields, req-121 c); the harness brake owns a desk's end when on.
        bool otherNow = (crouchTarget > 0f && time >= crouchTime && time < crouchTime + crouchHold)
            || (squatRise > 0f && time >= squatRiseTime && time < squatRiseTime + squatRiseHold)
            || (Mathf.Abs(leanTarget) > 0.01f && time >= leanTime && time < leanTime + leanHold)
            || (punchTarget > 0f && time >= punchTime && time < punchTime + punchHold)
            || (kickTarget > 0f && time >= kickTime && time < kickTime + kickHold);
        // DELEGATION req-272: with keys on, a squat desk is `other` for the walk key,
        // as S is in the game path - one release rule, not a second one.
        if (keysOn && walkPlanKind.Length > 0)
        {
            char keyDeskNow = WalkPlanKindAt(time);
            if (keyDeskNow == 's' || keyDeskNow == 'r') otherNow = true;
        }
        if (walkPlanBrake <= 0f && poseDrive != null)
            walkWindow = poseDrive.ClReleasePhaseKey(walkWindow, otherNow);
        bool runWindow = runTarget > 0.5f && time >= runTime && time < runTime + runHold;
        // CYCLE 97: the stop brake keeps the walk alive past a w desk's end (see
        // WalkPlanBraking); with the brake off every earlier path is bit for bit.
        bool braking = WalkPlanBraking(time);
        bool active = walkWindow || runWindow || braking;
        if (!active)
        {
            intent.run = 0f;
            intent.moveX = 0f;
            if (stepDriver.walkActive)
                stepDriver.EndWalk();
            else
                intent.standLeg = 0f;
            return;
        }

        intent.moveX = braking ? WalkPlanBrakeMoveX(time) : 1f;
        intent.run = runWindow ? 1f : 0f;
        // Entry-gate hold (-clEntryMask 1): a key the ready gate holds back must not start the step
        // driver either - no BeginWalk, no Tick; the EndWalk branch, while the real key stays in
        // moveXRaw for the gate's clock. Braking frames carry no key and are left alone.
        if (intent.holdWalk && (walkWindow || runWindow))
        {
            intent.run = 0f;
            if (stepDriver.walkActive)
                stepDriver.EndWalk();
            else
                intent.standLeg = 0f;
            return;
        }
        // The coast's press (-clReleaseRampPress): a press that ends the live coast (the command at
        // or under the switch's bar) is the entry gate's press from a stand from this tick on - the
        // step driver is not started before the gate opens (no BeginWalk, no Tick, not for one
        // tick); the held key stays in moveXRaw for the gate's clock. False without the switch.
        if (poseDrive != null && poseDrive.ClReleaseRampPressEnds)
        {
            intent.run = 0f;
            if (stepDriver.walkActive)
                stepDriver.EndWalk();
            else
                intent.standLeg = 0f;
            return;
        }
        // CYCLE 97b: the brake's own deceleration seat - the walk's braking catch.
        // ReflexCatchShare 0.5 = the landed law; toward the arrest point the swing
        // foot lands closer to where the CoM would stop, so each braked step bleeds
        // surplus speed. Written ONLY while a brake runs (restored once after), so
        // takes without the brake are bit for bit and the reflex keeps its writer.
        if (poseDrive != null)
        {
            if (braking)
            {
                poseDrive.ReflexCatchShare = Mathf.Lerp(0.5f, walkPlanBrakeCatch, WalkPlanBrakeU(time));
                brakeCatchWrote = true;
            }
            else if (brakeCatchWrote)
            {
                poseDrive.ReflexCatchShare = 0.5f;
                brakeCatchWrote = false;
            }
        }

        if (!stepDriver.walkActive)
            stepDriver.BeginWalk(runWindow ? runTime
                : (walkPlanKind.Length > 0 ? WalkPlanStartAt(time) : walkTime));
        stepDriver.stanceDuration = walkStanceDuration;
        stepDriver.runStanceDuration = runStanceDuration;
        stepDriver.runTransferMaxDuration = runTransferMaxDuration;
        stepDriver.stanceHeelStrikeMinAge = walkHeelStrikeMinAge;
        stepDriver.stanceHeelAirMin = walkHeelAirMin;
        stepDriver.stanceComTrigger = walkComTrigger;
        stepDriver.stanceComMinAge = walkComTriggerMinAge;
        stepDriver.stanceComTriggerPerSpeed = walkComTriggerPerSpeed;
        stepDriver.walkComTriggerMinStanceFraction = GetFloatArg("-walkComTriggerMinStanceFraction", stepDriver.walkComTriggerMinStanceFraction);
        stepDriver.walkTiltAbortDeg = GetFloatArg("-walkTiltAbort", stepDriver.walkTiltAbortDeg);
        stepDriver.walkTiltRecoverDeg = GetFloatArg("-walkTiltRecover", stepDriver.walkTiltRecoverDeg);
        stepDriver.walkCadenceStanceGain = walkCadenceStanceGain;
        stepDriver.walkCadenceMinStance = walkCadenceMinStance;
        stepDriver.transferMaxDuration = walkTransferMaxDuration;
        stepDriver.transferMinDuration = walkTransferMinDuration;
        stepDriver.transferTouchdownMaxAge = walkTransferTouchdownMaxAge;
        stepDriver.walkCadenceTransferGain = walkCadenceTransferGain;
        stepDriver.walkCadenceMinTransfer = walkCadenceMinTransfer;
        stepDriver.transferComOffsetMax = walkTransferComMax;
        stepDriver.transferFallbackComMax = walkTransferFallbackComMax;
        stepDriver.transferStandLegLevelMax = walkTransferLevelMax;
        stepDriver.firstStance = walkFirstStance;
        ApplyFallCatchWalkFields(stepDriver);
        stepDriver.Tick(time);
    }

    // Clocked one-leg: left up, plant, right up, plant. Not walkActive,
    // not WalkLift, not a forward wish. Plant must outlast
    // standLegReleaseRatePerSecond (4/s → 0.25 s) so heldStandCmd can
    // decay before the opposite sign, same as the one-leg release fix.
    private void UpdateMarchIntent(float time)
    {
        if (intent == null) return;

        bool window = marchTarget > 0.5f
            && time >= marchTime && time < marchTime + marchDuration;
        intent.march = window ? 1f : 0f;
        intent.moveX = 0f;
        if (!window)
        {
            intent.standLeg = 0f;
            return;
        }

        float hold = Mathf.Max(0.1f, marchHold);
        float plant = Mathf.Max(0.25f, marchPlant);
        float half = hold + plant;
        float cycle = 2f * half;
        float pos = (time - marchTime) % cycle;
        float sign0 = marchFirst < 0f ? -1f : 1f;
        if (pos < hold)
            intent.standLeg = sign0;
        else if (pos < hold + plant)
            intent.standLeg = 0f;
        else if (pos < hold + plant + hold)
            intent.standLeg = -sign0;
        else
            intent.standLeg = 0f;
    }

    private void ApplyFallCatchWalkFields(StepPhaseDriver driver)
    {
        if (driver == null) return;
        // Play never runs this: it uses StepPhaseDriver field defaults.
        // Keep CLI fallbacks in Start() equal to those fields, except
        // useFallCatchWalk (0 here, 1 on the driver for Play/WebGL).
        driver.useFallCatchWalk = useFallCatchWalk;
        driver.fallComLeadTrigger = fallComLeadTrigger;
        driver.fallPelvisLean = fallPelvisLean;
        driver.fallForwardLeanScale = fallForwardLeanScale;
        driver.catchKneeFlex = catchKneeFlex;
        driver.catchLeadCapture = catchLeadCapture;
        driver.catchFootLeadMin = catchFootLeadMin;
        driver.catchLeadCaptureMargin = catchLeadCaptureMargin;
        driver.reachKneeFlex = reachKneeFlex;
        driver.fallStanceVault = fallStanceVault;
        driver.fallSwingThighHold = fallSwingThighHold;
        driver.fallSwingHipLift = fallSwingHipLift;
        driver.fallSwingAnkleLift = fallSwingAnkleLift;
        driver.fallSwingAirAnkle = fallSwingAirAnkle;
        driver.fallSwingAirHeel = fallSwingAirHeel;
        driver.fallSwingHeelPlant = fallSwingHeelPlant;
        driver.fallSwingScrapeExtend = fallSwingScrapeExtend;
        driver.fallSwingCatchLandKnee = fallSwingCatchLandKnee;
        driver.fallSwingPlantStanceKnee = fallSwingPlantStanceKnee;
        driver.fallSwingPlantMidStanceExtend = fallSwingPlantMidStanceExtend;
        driver.fallSwingVaultRamp = fallSwingVaultRamp;
        driver.fallSwingCatchVaultYield = fallSwingCatchVaultYield;
        driver.fallSwingArmClock = fallSwingArmClock;
        driver.fallSwingReachLiftClock = fallSwingReachLiftClock;
        driver.fallSwingReachArmClock = fallSwingReachArmClock;
        driver.fallSwingCatchArmClock = fallSwingCatchArmClock;
        driver.fallSwingCatchExtendClock = fallSwingCatchExtendClock;
        driver.fallSwingCatchReachResume = fallSwingCatchReachResume;
        driver.fallSwingCatchReachScrapeResume = fallSwingCatchReachScrapeResume;
        driver.fallSwingCatchReachScrapeLiftHold = fallSwingCatchReachScrapeLiftHold;
        driver.fallSwingCatchReachScrapeLiftAirAge = fallSwingCatchReachScrapeLiftAirAge;
        driver.fallSwingCatchReachScrapeVaultHold = fallSwingCatchReachScrapeVaultHold;
        driver.fallSwingCatchReachScrapeVaultAirAge = fallSwingCatchReachScrapeVaultAirAge;
        driver.fallSwingCatchReachScrapeArmHold = fallSwingCatchReachScrapeArmHold;
        driver.fallSwingCatchReachScrapeArmAirAge = fallSwingCatchReachScrapeArmAirAge;
        driver.fallSwingCatchReachScrapeBrakeHold = fallSwingCatchReachScrapeBrakeHold;
        driver.fallSwingCatchReachScrapeBrakeAirAge = fallSwingCatchReachScrapeBrakeAirAge;
        driver.fallSwingCatchReachScrapeYieldHold = fallSwingCatchReachScrapeYieldHold;
        driver.fallSwingCatchReachScrapeYieldAirAge = fallSwingCatchReachScrapeYieldAirAge;
        driver.fallSwingCatchReachScrapeExtendHold = fallSwingCatchReachScrapeExtendHold;
        driver.fallSwingCatchReachScrapeExtendAirAge = fallSwingCatchReachScrapeExtendAirAge;
        driver.fallSwingCatchReachVaultClock = fallSwingCatchReachVaultClock;
        driver.fallSwingCatchReachVaultAge = fallSwingCatchReachVaultAge;
        driver.fallSwingCatchReachArmClock = fallSwingCatchReachArmClock;
        driver.fallSwingCatchReachLiftClock = fallSwingCatchReachLiftClock;
        driver.fallSwingCatchReachLiftAge = fallSwingCatchReachLiftAge;
        driver.fallSwingCatchReachArmAge = fallSwingCatchReachArmAge;
        driver.fallSwingCatchAirVaultHold = fallSwingCatchAirVaultHold;
        driver.fallSwingCatchAirVaultRemain = fallSwingCatchAirVaultRemain;
        driver.fallSwingCatchAirArmHold = fallSwingCatchAirArmHold;
        driver.fallSwingCatchAirExtendHold = fallSwingCatchAirExtendHold;
        driver.fallSwingCatchScrapeExtendHold = fallSwingCatchScrapeExtendHold;
        driver.fallSwingCatchScrapeExtendAirAge = fallSwingCatchScrapeExtendAirAge;
        driver.fallSwingCatchScrapeExtendAirEnterHold = fallSwingCatchScrapeExtendAirEnterHold;
        driver.fallSwingCatchReachScrapeExtendEnterHold = fallSwingCatchReachScrapeExtendEnterHold;
        driver.fallSwingCatchAirLiftHold = fallSwingCatchAirLiftHold;
        driver.fallSwingCatchScrapeLiftAirHold = fallSwingCatchScrapeLiftAirHold;
        driver.fallSwingCatchScrapeLiftAirEnterHold = fallSwingCatchScrapeLiftAirEnterHold;
        driver.fallSwingCatchScrapeLiftAirCatchHold = fallSwingCatchScrapeLiftAirCatchHold;
        driver.fallSwingCatchScrapeLiftAirCatchFold = fallSwingCatchScrapeLiftAirCatchFold;
        driver.fallSwingCatchScrapeLiftAirEnterFold = fallSwingCatchScrapeLiftAirEnterFold;
        driver.fallSwingCatchScrapeLiftAirEnterArm = fallSwingCatchScrapeLiftAirEnterArm;
        driver.fallSwingCatchScrapeLiftAirCatchArm = fallSwingCatchScrapeLiftAirCatchArm;
        driver.fallSwingCatchScrapeLiftAirCatchEndArm = fallSwingCatchScrapeLiftAirCatchEndArm;
        driver.fallSwingCatchScrapeLiftAirCatchGroundArm = fallSwingCatchScrapeLiftAirCatchGroundArm;
        driver.fallSwingCatchScrapeLiftAirCatchAirArm = fallSwingCatchScrapeLiftAirCatchAirArm;
        driver.fallSwingCatchScrapeLiftAirCatchAirEndArm = fallSwingCatchScrapeLiftAirCatchAirEndArm;
        driver.fallSwingCatchScrapeLiftAirCatchAirGroundArm = fallSwingCatchScrapeLiftAirCatchAirGroundArm;
        driver.fallSwingCatchScrapeLiftAirEnterGroundArm = fallSwingCatchScrapeLiftAirEnterGroundArm;
        driver.fallSwingCatchScrapeLiftAirEnterGroundFold = fallSwingCatchScrapeLiftAirEnterGroundFold;
        driver.fallSwingCatchScrapeLiftAirCatchGroundFold = fallSwingCatchScrapeLiftAirCatchGroundFold;
        driver.fallSwingCatchScrapeLiftAirCatchAirGroundFold = fallSwingCatchScrapeLiftAirCatchAirGroundFold;
        driver.fallSwingCatchScrapeLiftAirCatchAirFold = fallSwingCatchScrapeLiftAirCatchAirFold;
        driver.fallSwingCatchScrapeLiftAirCatchAirEnterFold = fallSwingCatchScrapeLiftAirCatchAirEnterFold;
        driver.fallSwingCatchScrapeLiftAirCatchAirEnterArm = fallSwingCatchScrapeLiftAirCatchAirEnterArm;
        driver.fallSwingCatchScrapeLiftAirCatchAirEnterGroundArm = fallSwingCatchScrapeLiftAirCatchAirEnterGroundArm;
        driver.fallSwingCatchScrapeLiftAirCatchAirEnterGroundFold = fallSwingCatchScrapeLiftAirCatchAirEnterGroundFold;
        driver.fallSwingCatchScrapeLiftAirCatchAirEnterEndArm = fallSwingCatchScrapeLiftAirCatchAirEnterEndArm;
        driver.fallSwingCatchScrapeLiftAirCatchAirEnterCatchArm = fallSwingCatchScrapeLiftAirCatchAirEnterCatchArm;
        driver.fallSwingCatchScrapeLiftAirCatchAirEnterCatchFold = fallSwingCatchScrapeLiftAirCatchAirEnterCatchFold;
        driver.fallSwingCatchScrapeLiftAirCatchAirEnterEndFold = fallSwingCatchScrapeLiftAirCatchAirEnterEndFold;
        driver.fallSwingCatchScrapeLiftAirCatchEndFold = fallSwingCatchScrapeLiftAirCatchEndFold;
        driver.fallSwingCatchScrapeLiftAirCatchGroundCatchFold = fallSwingCatchScrapeLiftAirCatchGroundCatchFold;
        driver.fallSwingCatchScrapeLiftAirCatchGroundCatchEndFold = fallSwingCatchScrapeLiftAirCatchGroundCatchEndFold;
        driver.fallSwingCatchScrapeLiftAirCatchAirEndFold = fallSwingCatchScrapeLiftAirCatchAirEndFold;
        driver.fallSwingCatchScrapeLiftAirCatchAirGroundCatchFold = fallSwingCatchScrapeLiftAirCatchAirGroundCatchFold;
        driver.fallSwingCatchScrapeLiftAirCatchPlantExitFold = fallSwingCatchScrapeLiftAirCatchPlantExitFold;
        driver.fallSwingPlantStanceExtendClock = fallSwingPlantStanceExtendClock;
        driver.fallSwingVaultFromLeftover = fallSwingVaultFromLeftover;
        driver.fallSwingFallAnklePush = fallSwingFallAnklePush;
        driver.fallSwingFallHipLiftCap = fallSwingFallHipLiftCap;
        driver.fallSwingFallAnkleLift = fallSwingFallAnkleLift;
        driver.fallSwingFallHipLift = fallSwingFallHipLift;
        driver.fallSwingReachAirHold = fallSwingReachAirHold;
        driver.fallSwingFallAirHold = fallSwingFallAirHold;
        driver.fallSwingFallHipLiftCapWide = fallSwingFallHipLiftCapWide;
        driver.fallSwingReachHipLiftCapWide = fallSwingReachHipLiftCapWide;
        driver.fallSwingCatchAirHipLiftCapWide = fallSwingCatchAirHipLiftCapWide;
        driver.fallSwingPlantHipLiftCapWide = fallSwingPlantHipLiftCapWide;
        driver.fallSwingFallHipLiftCapCeil = fallSwingFallHipLiftCapCeil;
        driver.fallSwingReachHipLiftCapCeil = fallSwingReachHipLiftCapCeil;
        driver.fallSwingCatchAirHipLiftCapCeil = fallSwingCatchAirHipLiftCapCeil;
        driver.fallSwingPlantHipLiftCapCeil = fallSwingPlantHipLiftCapCeil;
        driver.fallSwingCatchScrapeHipLiftCapCeil = fallSwingCatchScrapeHipLiftCapCeil;
        driver.fallSwingCatchReachScrapeHipLiftCapCeil = fallSwingCatchReachScrapeHipLiftCapCeil;
        driver.fallSwingCatchAirHold = fallSwingCatchAirHold;
        driver.fallSwingCatchAirEnterHold = fallSwingCatchAirEnterHold;
        driver.fallSwingPlantAirHold = fallSwingPlantAirHold;
        driver.fallSwingReachAirSwingCeil = fallSwingReachAirSwingCeil;
        driver.fallSwingHipairCeil = fallSwingHipairCeil;
        driver.fallSwingRecoverHipairClear = fallSwingRecoverHipairClear;
        driver.fallSwingRecoverAirAnkleClear = fallSwingRecoverAirAnkleClear;
        driver.fallSwingPlantAirAnkleClear = fallSwingPlantAirAnkleClear;
        driver.fallSwingPlantAirAnkleLevel = fallSwingPlantAirAnkleLevel;
        driver.fallSwingRecoverAirKneeClear = fallSwingRecoverAirKneeClear;
        driver.fallSwingReachHipLiftCap = fallSwingReachHipLiftCap;
        driver.fallSwingReachHipLift = fallSwingReachHipLift;
        driver.fallSwingReachAnkleLift = fallSwingReachAnkleLift;
        driver.fallSwingReachHipLiftCapHold = fallSwingReachHipLiftCapHold;
        driver.fallSwingFallHipLiftCapHold = fallSwingFallHipLiftCapHold;
        driver.fallSwingFallVaultLead = fallSwingFallVaultLead;
        driver.fallSwingFallLiftLead = fallSwingFallLiftLead;
        driver.fallSwingFallFoldLead = fallSwingFallFoldLead;
        driver.fallSwingCatchAirHipLiftCap = fallSwingCatchAirHipLiftCap;
        driver.fallSwingCatchAirHipLiftCapHold = fallSwingCatchAirHipLiftCapHold;
        driver.fallSwingCatchAirHipLift = fallSwingCatchAirHipLift;
        driver.fallSwingCatchAirAnkleLift = fallSwingCatchAirAnkleLift;
        driver.fallSwingCatchScrapeHipLiftCap = fallSwingCatchScrapeHipLiftCap;
        driver.fallSwingCatchScrapeHipLiftCapHold = fallSwingCatchScrapeHipLiftCapHold;
        driver.fallSwingCatchScrapeHipLift = fallSwingCatchScrapeHipLift;
        driver.fallSwingCatchScrapeAnkleLift = fallSwingCatchScrapeAnkleLift;
        driver.fallSwingCatchScrapeLiftLead = fallSwingCatchScrapeLiftLead;
        driver.fallSwingCatchScrapeHipLiftCapWide = fallSwingCatchScrapeHipLiftCapWide;
        driver.fallSwingCatchReachScrapeHipLiftCap = fallSwingCatchReachScrapeHipLiftCap;
        driver.fallSwingCatchReachScrapeHipLiftCapHold = fallSwingCatchReachScrapeHipLiftCapHold;
        driver.fallSwingCatchReachScrapeHipLift = fallSwingCatchReachScrapeHipLift;
        driver.fallSwingCatchReachScrapeAnkleLift = fallSwingCatchReachScrapeAnkleLift;
        driver.fallSwingCatchReachScrapeHipLiftCapWide = fallSwingCatchReachScrapeHipLiftCapWide;
        driver.plantMinDuration = plantMinDuration;
        driver.plantLeadWaitSeconds = plantLeadWaitSeconds;
        driver.reachTimeout = reachTimeout;
        driver.fallAbortDeg = fallAbortDeg;
        driver.plantStallReseatSeconds = plantStallReseatSeconds;
        driver.plantReseatLeadMargin = plantReseatLeadMargin;
        driver.plantStallStandSeconds = plantStallStandSeconds;
        driver.plantClearRestoreOnGate = plantClearRestoreOnGate;
        driver.reachGroundStallSeconds = reachGroundStallSeconds;
        driver.fallCatchBounceMax = fallCatchBounceMax;
        driver.sinkAbortDrop = sinkAbortDrop;
        driver.plantAirLowestFoot = plantAirLowestFoot;
        driver.walkRecoverAnyPose = walkRecoverAnyPose;
        // Recover→Plant reads recoverComOffsetMax, not transferComOffsetMax.
        // The latter is the legacy mid-support Transfer band and is assigned
        // above; it never entered this method, so a -walkTransferCom sweep
        // changed IsTransferReady only (unused on the fall-catch Recover
        // exit). Same CLI, this field, catch-foot metric.
        driver.recoverComOffsetMax = GetFloatArg("-walkTransferCom", driver.recoverComOffsetMax);
        // Caught band shared by the Plant roll latch and the CoP arrest.
        // Recover swap does not wait for it. -plantCaughtLead 0 restores
        // the former "CoM behind the ankle" latch, bit for bit.
        driver.plantCaughtLeadMax = GetFloatArg(
            "-plantCaughtLead", driver.plantCaughtLeadMax);
        // Ceiling on the lead-arrest half of that brake. -plantArrestCop 0
        // removes the arrest (trunk term only), 1 is the saturating floor
        // that bounced the trunk to +22° backward.
        driver.plantArrestCopScale = GetFloatArg(
            "-plantArrestCop", driver.plantArrestCopScale);
        // Runaway escape: Plant leaves for Reach and the aborts stop
        // preferring a two-foot stand while the CoM is past the support.
        // -walkKeepStepping 0 restores the former ladder (brake, re-seat,
        // surrender) bit for bit.
        driver.walkRunawayKeepStepping = GetFloatArg(
            "-walkKeepStepping", driver.walkRunawayKeepStepping);
        driver.plantRunawayStepSeconds = GetFloatArg(
            "-plantRunawayStep", driver.plantRunawayStepSeconds);
    }

    // left / −1 — left support, right / +1 — right, 0 / both — both feet.
    // A string, not a float: otherwise “left” silently falls back to the default, like a comma in a number.
    private void UpdateStandLegIntent(float time, ref bool marked, ref bool released)
    {
        if (intent == null) return;

        bool holding = Mathf.Abs(standLegTarget) > 0.5f
            && time >= standLegTime && time < standLegTime + standLegHold;
        intent.standLeg = holding ? standLegTarget : 0f;

        if (holding && !marked)
        {
            recorder.MarkStandLeg(time, standLegTarget);
            marked = true;
        }
        else if (marked && !released && !holding)
        {
            recorder.MarkStandLegRelease(time);
            released = true;
        }
    }

    private static float ParseStandLegArg(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return 0f;

        string key = raw.Trim().ToLowerInvariant();
        if (key == "left" || key == "l") return -1f;
        if (key == "right" || key == "r") return 1f;
        if (key == "both" || key == "none" || key == "0") return 0f;

        if (float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out float value))
        {
            if (value > 0.5f) return 1f;
            if (value < -0.5f) return -1f;
            return 0f;
        }

        Debug.LogWarning($"HeadlessTrial: could not parse -standLeg={raw}, using 0 (both feet).");
        return 0f;
    }

    private void Finish(float simulated, float wallSeconds = -1f)
    {
        try
        {
            Directory.CreateDirectory(outputFolder);

            string csvPath = Path.Combine(outputFolder, label + ".csv");
            string summary;
            if (spawnDog)
            {
                File.WriteAllText(csvPath, dogRecorder.BuildCsv());
                summary = dogRecorder.BuildSummaryJson(simulated, wallSeconds);
            }
            else if (spawnBird)
            {
                File.WriteAllText(csvPath, birdRecorder.BuildCsv());
                summary = birdRecorder.BuildSummaryJson(simulated, wallSeconds);
                // B-NAT-0: the events log and the onset arithmetic land beside the rows.
                File.WriteAllText(
                    Path.Combine(outputFolder, label + "_events.csv"),
                    birdRecorder.BuildEventsCsv());
                File.WriteAllText(
                    Path.Combine(outputFolder, label + "_onset.json"),
                    birdRecorder.BuildOnsetJson());
                if (plantRecorder != null)
                {
                    summary = MergePlantJson(summary, plantRecorder);
                    WriteJointTrace(label, plantRecorder.BuildJointCsv());
                }
            }
            else if (spawnTree)
            {
                File.WriteAllText(csvPath, "t\n");
                summary = plantRecorder.BuildSummaryJson(simulated, wallSeconds);
                // T1: one row per FixedUpdate, ten columns per joint.
                WriteJointTrace(label, plantRecorder.BuildJointCsv());
                // The owner's landing sheet (25.09): the ring itself on disk, so
                // the bird lane can read where a bird may land without a helper.
                WriteLandingCsv(label, plantTree);
                // LND2 (26.09): the ring's own open-air read and the wood it
                // stands on, so the numbers travel with the run.
                WriteSkyCsv(label, plantTree);
                WriteWoodCsv(label, plantTree);
            }
            else if (spawnApple)
            {
                File.WriteAllText(csvPath, "t\n");
                summary = appleRecorder.BuildSummaryJson(simulated, wallSeconds);
                // T1: one row per FixedUpdate, ten columns per joint.
                WriteJointTrace(label, appleRecorder != null ? appleRecorder.BuildJointCsv() : null);
            }
            else if (spawnFlower)
            {
                File.WriteAllText(csvPath, "t\n");
                summary = flowerRecorder.BuildSummaryJson(simulated, wallSeconds);
            }
            else if (spawnArena)
            {
                File.WriteAllText(csvPath, arena.BuildHumanCsv());
                summary = arena.WriteFiles(simulated, wallSeconds);
            }
            else if (spawnDogArena)
            {
                // DOG ARENA (cycle 64): the runner writes its own files (the deterministic rows,
                // the reference trace, the append-only parameter log) and returns the summary; the
                // label.csv keeps the same shape every trial's table has.
                File.WriteAllText(csvPath, dogArena.BuildCsv());
                summary = dogArena.FinishAndSummary(simulated, wallSeconds);
            }
            else
            {
                File.WriteAllText(csvPath, recorder.BuildCsv());
                summary = recorder.BuildSummaryJson(simulated, wallSeconds);
                // WALK CYCLE 47: the per-plant section of THIS run, next to the
                // summary JSON. A human row with no plants (the quiet stance, or
                // a run whose walk never latched) carries the header alone - so
                // both rows of the pair carry the columns without either row's
                // own .csv losing a byte.
                WritePlantSection(label);
                // DROP TEST (line 3, 27.09): the release record beside the rows.
                // Written only when -drop was given, so no existing file gains a byte.
                if (dropHeight > 0f)
                    WriteDropSection(label);
                // TILT STAND (29.09): the per-step log of the rotating deck beside
                // the rows, and its own section in the summary.
                if (tiltLog != null)
                {
                    WriteTiltSection(label);
                    summary = MergeTiltJson(summary, tiltLog);
                }
            }
            // LEVEL 1 (req-217 step 2): the crow rows beside the take. Written only
            // under -level1, so no existing take gains a byte.
            if (level1Crows != null)
                WriteLevel1Section(label);
            // WORLD-1 (req-285): the world's own three files, beside the take.
            if (worldHost != null)
                worldHost.rec.Write(outputFolder, label);
            // DELEGATION req-272: the take's own end row, then the key log is closed.
            if (keysOn)
                CloseKeyLog();

            // req-303: the take's own argument audit; writes nothing unless -argsAudit 1.
            ArgsAudit.Write(label, outputFolder);

            File.WriteAllText(Path.Combine(outputFolder, label + ".json"), summary);

            if (impactRecorder != null)
                impactRecorder.WriteFiles(simulated, wallSeconds);
            if (ballRecorder != null)
                ballRecorder.WriteFiles(simulated, wallSeconds);

            // Marker in stdout so the result can be read straight from the run log.
            Debug.Log("TRIAL_SUMMARY " + summary);
            Debug.Log("TRIAL_CSV " + csvPath);
        }
        catch (Exception e)
        {
            Debug.LogError("HeadlessTrial: failed to write results: " + e.Message);
            Application.Quit(2);
            return;
        }

        Application.Quit(0);
    }

    // T1 (TREES plan): the per-step, per-joint trace of the tree run, written
    // next to the summary JSON. Read-only instrumentation — the run's own .csv
    // keeps its old content, so nothing that already parses it breaks. Both
    // hingenous trees (oak and apple) hand out the same CSV layout.
    // WALK CYCLE 47: <label>_plants.csv - one row per plant the walk lane's own
    // latch accepted (see WalkPlantLog). Read-only instrumentation: the run's own
    // .csv is written above and keeps every byte it had.
    private void WritePlantSection(string label)
    {
        string path = Path.Combine(outputFolder, label + "_plants.csv");
        File.WriteAllText(path, WalkPlantLog.BuildCsv());
        Debug.Log("TRIAL_PLANTS_CSV " + path + " plants=" + WalkPlantLog.Count);
    }

    // DROP TEST (line 3, 27.09): <label>_drop.json - the release moment and the rig
    // size as THIS run saw them, so the analysis reads the release from a file
    // instead of parsing the log. The state itself stays in the usual CSV rows.
    private void WriteDropSection(string label)
    {
        StringBuilder json = new StringBuilder(256);
        json.Append("{\"label\":\"").Append(label).Append('"');
        json.Append(",\"dropHeight\":").Append(dropHeight.ToString("0.####", CultureInfo.InvariantCulture));
        json.Append(",\"dropTime\":").Append(dropTime.ToString("0.####", CultureInfo.InvariantCulture));
        json.Append(",\"releaseTime\":").Append(dropReleaseTime.ToString("0.#####", CultureInfo.InvariantCulture));
        json.Append(",\"releaseStep\":").Append(dropReleaseStep);
        json.Append(',').Append('"').Append("bodies").Append('"').Append(':').Append(dropBodyCount);
        json.Append(',').Append('"').Append("fixedDelta").Append('"').Append(':')
            .Append(Time.fixedDeltaTime.ToString("0.#####", CultureInfo.InvariantCulture));
        // LANDING TRANSIENT (round 2, 27.09): what the balance core's own landing
        // phase saw on this run - the proof it fired (a non-zero contact count)
        // and the measured arrival speed it folded against. Written only when
        // -drop was given, so no existing file gains a byte.
        BalanceController landingCore = human != null
            ? human.GetComponent<BalanceController>() : null;
        if (landingCore != null)
        {
            json.Append(',').Append('"').Append("landing").Append('"').Append(':').Append('{');
            json.Append('"').Append("enabled").Append('"').Append(':')
                .Append(landingCore.landingTransient ? "true" : "false");
            json.Append(',').Append('"').Append("contacts").Append('"').Append(':')
                .Append(landingCore.LandingContactCount);
            json.Append(',').Append('"').Append("maxImpactSpeed").Append('"').Append(':')
                .Append(landingCore.LandingMaxImpactSpeed.ToString("0.####", CultureInfo.InvariantCulture));
            json.Append(',').Append('"').Append("maxYieldLevel").Append('"').Append(':')
                .Append(landingCore.LandingMaxYieldLevel.ToString("0.####", CultureInfo.InvariantCulture));
            json.Append(',').Append('"').Append("maxCatchBrakeDeg").Append('"').Append(':')
                .Append(landingCore.LandingMaxCatchBrake.ToString("0.####", CultureInfo.InvariantCulture));
            json.Append(',').Append('"').Append("catchFrames").Append('"').Append(':')
                .Append(landingCore.LandingCatchFrames);
            json.Append(',').Append('"').Append("liveFrames").Append('"').Append(':')
                .Append(landingCore.LandingLiveFrames);
            json.Append('}');
        }
        json.Append('}');
        string path = Path.Combine(outputFolder, label + "_drop.json");
        File.WriteAllText(path, json.ToString());
        Debug.Log("TRIAL_DROP_JSON " + path);
    }

    // TILT STAND (29.09): <label>_tiltsteps.csv - one row per step of the deck's ramp
    // (see TiltStandLog.cs). Read-only: the run's own .csv keeps every byte.
    private void WriteTiltSection(string label)
    {
        string path = Path.Combine(outputFolder, label + "_tiltsteps.csv");
        File.WriteAllText(path, tiltLog.BuildCsv());
        Debug.Log("TILT STAND " + tiltLog.ReportLine());
        Debug.Log("TRIAL_TILT_CSV " + path + " steps=" + tiltLog.StepCount);
    }

    private static string MergeTiltJson(string summary, TiltStandLog log)
    {
        if (string.IsNullOrEmpty(summary) || log == null)
            return summary;
        int close = summary.LastIndexOf('}');
        if (close < 0)
            return summary;
        return summary.Substring(0, close) + log.SummaryFragment() + summary.Substring(close);
    }

    private void WriteJointTrace(string label, string csv)
    {
        if (string.IsNullOrEmpty(csv)) return;
        string jointsPath = Path.Combine(outputFolder, label + "_joints.csv");
        File.WriteAllText(jointsPath, csv);
        Debug.Log("TRIAL_JOINTS_CSV " + jointsPath);
    }

    // The landing ring as rows: index, kind (top / fork / span), the world point
    // at the grow pose and the radius of the trigger circle. This is the sheet's
    // handshake for the bird lane — the points a bird may land on, in the order
    // GetLandingSlots() hands them out.
    private void WriteLandingCsv(string label, PlantTree tree)
    {
        if (tree == null || tree.LandingTriggerCount <= 0)
            return;
        Vector2[] slots = tree.GetLandingSlots();
        StringBuilder csv = new StringBuilder(1024);
        csv.Append("index,kind,x,y,r\n");
        for (int i = 0; i < slots.Length; i++)
        {
            csv.Append(i.ToString(CultureInfo.InvariantCulture)).Append(',')
                .Append(tree.GetLandingKind(i).ToString().ToLowerInvariant()).Append(',')
                .Append(slots[i].x.ToString("0.########", CultureInfo.InvariantCulture)).Append(',')
                .Append(slots[i].y.ToString("0.########", CultureInfo.InvariantCulture)).Append(',')
                .Append(tree.LandingTriggerRadiusM.ToString("0.########", CultureInfo.InvariantCulture))
                .Append('\n');
        }
        string path = Path.Combine(outputFolder, label + "_landing.csv");
        File.WriteAllText(path, csv.ToString());
        Debug.Log("TRIAL_LANDING_CSV " + path + " points=" + slots.Length);
    }

    // LND2 (26.09): the ring's own open-air read per mark — index, kind, the
    // mark's point, the seat plane under its centre column, the free window of
    // the claw band (the smallest of the three columns), the three columns
    // themselves (l, c, r) and how many are open at LandingSkyMinM. Written
    // beside the landing CSV; nothing without a ring (or before the recorder's
    // first sample has taken the read).
    private void WriteSkyCsv(string label, PlantTree tree)
    {
        if (tree == null || tree.LandingSkyCount <= 0)
            return;
        Vector2[] slots = tree.GetLandingSlots();
        StringBuilder csv = new StringBuilder(1024);
        csv.Append("index,kind,x,y,seatY,sky,bandL,bandC,bandR,openCols,wood\n");
        int n = tree.LandingSkyCount;
        for (int i = 0; i < n && i < slots.Length; i++)
        {
            int openCols = 0;
            for (int k = 0; k < 3; k++)
            {
                if (tree.GetLandingSkyColumn(i, k) >= PlantTree.LandingSkyMinM)
                    openCols++;
            }
            csv.Append(i.ToString(CultureInfo.InvariantCulture)).Append(',')
                .Append(tree.GetLandingKind(i).ToString().ToLowerInvariant()).Append(',')
                .Append(slots[i].x.ToString("0.########", CultureInfo.InvariantCulture)).Append(',')
                .Append(slots[i].y.ToString("0.########", CultureInfo.InvariantCulture)).Append(',')
                .Append(tree.GetLandingSeatY(i).ToString("0.########", CultureInfo.InvariantCulture)).Append(',')
                .Append(tree.GetLandingSky(i).ToString("0.########", CultureInfo.InvariantCulture)).Append(',')
                .Append(tree.GetLandingSkyColumn(i, 0).ToString("0.########", CultureInfo.InvariantCulture)).Append(',')
                .Append(tree.GetLandingSkyColumn(i, 1).ToString("0.########", CultureInfo.InvariantCulture)).Append(',')
                .Append(tree.GetLandingSkyColumn(i, 2).ToString("0.########", CultureInfo.InvariantCulture)).Append(',')
                .Append(openCols.ToString(CultureInfo.InvariantCulture)).Append(',')
                .Append(tree.GetLandingWoodName(i))
                .Append('\n');
        }
        string path = Path.Combine(outputFolder, label + "_sky.csv");
        File.WriteAllText(path, csv.ToString());
        Debug.Log("TRIAL_SKY_CSV " + path + " marks=" + n);
    }

    // LND2 (26.09): the plant's own geometry — one row per wood piece (name,
    // order, world pose, size, z-angle, whether it has a body). Read at the end
    // of the run, so the pose is the end pose; the sky read itself is taken at
    // the first sample. Both travel with the label, so the ring's numbers can be
    // checked without the bird lane's files.
    private void WriteWoodCsv(string label, PlantTree tree)
    {
        if (tree == null || tree.LandingTriggerCount <= 0)
            return;
        List<WoodSensor> wood = new List<WoodSensor>(32);
        tree.GetWoodSensors(wood);
        StringBuilder csv = new StringBuilder(1024);
        csv.Append("name,order,x,y,sizeX,sizeY,angleDeg,dynamic\n");
        for (int i = 0; i < wood.Count; i++)
        {
            HumanSegment seg = wood[i].segment;
            if (seg == null)
                continue;
            Vector3 p = seg.transform.position;
            csv.Append(seg.name).Append(',')
                .Append(wood[i].order.ToString(CultureInfo.InvariantCulture)).Append(',')
                .Append(p.x.ToString("0.########", CultureInfo.InvariantCulture)).Append(',')
                .Append(p.y.ToString("0.########", CultureInfo.InvariantCulture)).Append(',')
                .Append(seg.size.x.ToString("0.########", CultureInfo.InvariantCulture)).Append(',')
                .Append(seg.size.y.ToString("0.########", CultureInfo.InvariantCulture)).Append(',')
                .Append(seg.transform.eulerAngles.z.ToString("0.###", CultureInfo.InvariantCulture)).Append(',')
                .Append(seg.rb != null ? "1" : "0").Append('\n');
        }
        string path = Path.Combine(outputFolder, label + "_wood.csv");
        File.WriteAllText(path, csv.ToString());
        Debug.Log("TRIAL_WOOD_CSV " + path + " pieces=" + wood.Count);
    }

    private static string MergePlantJson(string summary, PlantTrialRecorder plant)
    {
        if (string.IsNullOrEmpty(summary) || plant == null)
            return summary;
        int close = summary.LastIndexOf('}');
        if (close < 0)
            return summary;
        StringBuilder extra = new StringBuilder(256);
        extra.Append(",\"treePresent\":true,\"tree\":{");
        StringBuilder inner = new StringBuilder(256);
        plant.AppendMetrics(inner);
        string body = inner.ToString();
        extra.Append(body.StartsWith(",") ? body.Substring(1) : body);
        extra.Append('}');
        return summary.Substring(0, close) + extra + summary.Substring(close);
    }

    private bool TryAcceptProvenance(out TrialRunInfo info, out string error)
    {
        info = null;
        error = null;

        BuildManifestDto manifest = LoadBuildManifest();
        string argFingerprint = GetStringArg("-sourceFingerprint", "");
        string manifestFingerprint = manifest != null ? manifest.fingerprint : "";
        string manifestGuid = manifest != null ? manifest.buildGuid : "";
        string appGuid = Application.buildGUID;

        if (!string.IsNullOrEmpty(argFingerprint) && !string.IsNullOrEmpty(manifestFingerprint)
            && argFingerprint != manifestFingerprint)
        {
            error = "sourceFingerprint arg != build-manifest.fingerprint";
            return false;
        }

        if (!string.IsNullOrEmpty(appGuid) && !string.IsNullOrEmpty(manifestGuid)
            && NormalizeGuid(appGuid) != NormalizeGuid(manifestGuid))
        {
            error = "Application.buildGUID != build-manifest.buildGuid";
            return false;
        }

        string fingerprint = !string.IsNullOrEmpty(manifestFingerprint)
            ? manifestFingerprint
            : (argFingerprint ?? "");

        info = new TrialRunInfo
        {
            Label = label,
            Duration = duration,
            StartY = startY,
            RequestedPushImpulse = pushImpulse,
            RequestedPushTime = pushTime,
            RequestedCrouch = crouchTarget,
            RequestedCrouchTime = crouchTime,
            RequestedCrouchHold = crouchHold,
            RequestedLean = leanTarget,
            RequestedLeanTime = leanTime,
            RequestedLeanHold = leanHold,
            RequestedStandLeg = standLegTarget,
            RequestedStandLegTime = standLegTime,
            RequestedStandLegHold = standLegHold,
            RequestedMarch = marchTarget,
            RequestedMarchTime = marchTime,
            RequestedMarchDuration = marchDuration,
            RequestedMarchHold = marchHold,
            RequestedMarchPlant = marchPlant,
            RequestedMarchFirst = marchFirst,
            RequestedWalk = walkTarget,
            RequestedWalkTime = walkTime,
            RequestedWalkDuration = walkDuration,
            RequestedWalkStance = walkStanceDuration,
            RequestedRun = runTarget,
            RequestedRunTime = runTime,
            RequestedRunHold = runHold,
            RequestedWalkTransfer = walkTransferMaxDuration,
            RequestedWalkTransferCom = walkTransferComMax,
            RequestedWalkTransferFallbackCom = walkTransferFallbackComMax,
            RequestedWalkTransferLevel = walkTransferLevelMax,
            RequestedWalkFirst = walkFirstStance,
            MuscleMultiplier = GetFloatArg("-muscle", 1f),
            FrictionMultiplier = GetFloatArg("-friction", 1f),
            SourceFingerprint = fingerprint ?? "",
            ManifestUnityVersion = manifest != null ? manifest.unityVersion : "",
            ManifestBuildGuid = manifestGuid ?? "",
            ManifestBuildTimeUtc = manifest != null ? manifest.buildTimeUtc : "",
            ManifestPath = FindBuildManifestPath() ?? "",
            CommandArgs = Environment.GetCommandLineArgs()
        };
        return true;
    }

    private static string NormalizeGuid(string value)
    {
        if (string.IsNullOrEmpty(value)) return "";
        return value.Replace("-", "").ToLowerInvariant();
    }

    private static BuildManifestDto LoadBuildManifest()
    {
        string path = FindBuildManifestPath();
        if (string.IsNullOrEmpty(path) || !File.Exists(path))
            return null;

        try
        {
            return JsonUtility.FromJson<BuildManifestDto>(File.ReadAllText(path));
        }
        catch (Exception e)
        {
            Debug.LogWarning("HeadlessTrial: failed to read build-manifest: " + e.Message);
            return null;
        }
    }

    private static string FindBuildManifestPath()
    {
        string fromArg = GetStringArg("-buildManifest", null);
        if (!string.IsNullOrEmpty(fromArg) && File.Exists(fromArg))
            return fromArg;

        try
        {
            string parent = Directory.GetParent(Application.dataPath)?.FullName;
            if (!string.IsNullOrEmpty(parent))
            {
                string nextToExe = Path.Combine(parent, "build-manifest.json");
                if (File.Exists(nextToExe))
                    return nextToExe;
            }
        }
        catch
        {
        }

        return null;
    }

    private static string DefaultOutputFolder()
    {
        try
        {
            string parent = Directory.GetParent(Application.dataPath)?.FullName;
            if (!string.IsNullOrEmpty(parent))
                return Path.Combine(parent, "trials");
        }
        catch
        {
        }

        return Path.Combine(Directory.GetCurrentDirectory(), "trials");
    }

    // ─── COMMAND-LINE ARGUMENT PARSING ───
    // WALK CYCLE 66: a body BORN foreshortened (the flag's own path — Human applies
    // the length before the build) stands on the ground with a shorter leg, so the
    // root drops by the thigh's own shortening. The ramp path is not dropped: that
    // body is born at full length (Human.ForeshortenPrepare). 0.43 is the rig's own
    // thigh length (Human.thighSize.y, the constant the -0.82 comment above spells
    // out); an explicit -startY still wins.
    private static float HumanForeshortenLegDrop()
    {
        if (Foreshorten <= 0f || ForeshortenRampAt >= 0f) return 0f;
        return 0.43f * Mathf.Clamp(Foreshorten, 0f, Human.ForeshortenMax);
    }

    private static string GetStringArg(string name, string fallback)
    {
        string[] args = Environment.GetCommandLineArgs();
        int index = Array.IndexOf(args, ArgsAudit.Note(name));
        if (index < 0 || index + 1 >= args.Length) return fallback;
        return args[index + 1];
    }

    private static bool HasArg(string name)
    {
        return Array.IndexOf(Environment.GetCommandLineArgs(), ArgsAudit.Note(name)) >= 0;
    }

    private static float GetFloatArg(string name, float fallback)
    {
        string raw = GetStringArg(name, null);
        if (string.IsNullOrEmpty(raw)) return fallback;

        // Invariant culture is required: on a Russian locale comma
        // and dot parse differently, and parameters silently slip.
        if (float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out float value))
            return value;

        Debug.LogWarning($"HeadlessTrial: could not parse {name}={raw}, using {fallback}.");
        return fallback;
    }
}
