using UnityEngine;

// Arm drive. Yury's rule: the arms work the vestibular apparatus, they are not
// decoration. This component owns the six arm muscle activations (flexor and
// extensor on both shoulders and both elbows) and writes them once per physics
// step: after BalanceController (order 0), before ActuatorDriver applies the
// muscles (order 50). Every other joint stays with the balance controller --
// including the wrist, EXCEPT while a press is running on that side, where the
// hand's own pair is taken over too (arms cycle 12: the press loads the palm
// through a joint with 12x the hand's authority, and the grip is measurably lost
// exactly then).
//
// Two jobs:
//
//  1. WALK. A reciprocal swing built from the measured hip angles, not from a
//     clock, so it holds on any gait this rig produces. The left shoulder is
//     driven against the RIGHT thigh and the right shoulder against the LEFT one
//     (the acceptance pairing), plus a smaller same-side term so that the two
//     arms stay in counter phase with each other even in a window where the legs
//     move together. The phase comes from the hips, the amplitude from the
//     balance controller's own swing range: no hardcoded sinusoid.
//
//  2. FALL. Per side: fold the arm down onto the slab, latch the palm's world
//     point, then press. Measured (p_arms20): with the target chasing the
//     shoulder the arm walked its hand along the floor — 191 strokes of 0.18 s,
//     2.7 deg of elbow extension, the palm 0.9 m in the air on the left side.
//     A press needs a FIXED palm: the aim holds the upper arm on the latched
//     point while the elbow extends, the arm becomes a strut, and the only way
//     the chain can give is the shoulder leaving that point — the body rising.
//     Real contact (Collider2D.IsTouchingLayers) starts the stroke and holds it:
//     a stroke is latched for pressMinStrokeSeconds and only a contact loss that
//     lasts pressLostContactSeconds may abandon it.
//
//     Two rules were measured wrong on p_arms39/40 and are now the opposite:
//     (1) the latched palm sits UNDER the shoulder (plantLateralShare 0), not
//     beside it — a side plant spends the stroke on a horizontal shove (the body
//     was pushed 2.0 m along the slab, pelvis never leaving -1.86); (2) the
//     stroke deepens the latched point (pressStrokeMode 1) instead of ramping the
//     elbow angle, because an elbow ramp leaves the hand's position uncontrolled:
//     the hand was measured 0.39 m past its own palm, walking along the floor.
//
//     Two more failed on p_arms50 and are now measured the other way:
//     (3) "under the shoulder" is only half of it — the point must be a CHORD the
//     arm can hold torque at. A lying shoulder 0.11 m over the slab latched a
//     point 0.11 m away against a 0.218 m shortest usable chord: the solve
//     clamped, the hand settled 0.32 m away from its own palm and the elbow
//     "extended" 60 deg into the air (shoulder rise 0.03 m). The offset below is
//     sqrt(reach^2 - drop^2), i.e. the palm is directly under the shoulder
//     whenever that is reachable and at the arm's own minimum chord when it is
//     not; (4) Hold must end when the elbow stops extending while the body is
//     still down, otherwise one plant spends the whole fall (5775 of 6447 fallen
//     frames in Hold, 2 strokes, no rise).
//
// Sign convention of this rig, checked against the recorded columns: jointAngle
// negative = flexion = limb forward. A forward-swinging thigh has a NEGATIVE hip
// jointAngle and a forward-swung arm has a NEGATIVE shoulder angle. A positive
// activation signal increases jointAngle (extensor work).
//
// Interface for the get-up, which lives in BalanceController (not this file):
//
//     armDrive.RequestArmPress(intensity);   // 0..1, call every step while GettingUp
//
// Returns true when both palms are in real contact; the return value is a
// convenience, the caller may ignore it. A request older than
// pressRequestHoldSeconds is dropped, so a get-up that stops asking cannot leave
// the arms latched. Per-side read-back for the get-up and the recorder:
// HandContactL/R, PressPhaseL/R (0 idle 1 lift 2 press 3 hold 4 plant),
// PressStrokeExtensionL/R, PressShoulderLiftL/R, PressLiftMax, PressStrokesL/R.
// The maxima are RUN-scoped since arms cycle 10 (a reset by BeginStroke cannot
// make a published maximum fall); the stroke in flight is read from
// PressLastStrokeExtensionL/R, PressLastStrokeShoulderExtL/R and
// PressLastStrokeShoulderLiftL/R.
[DefaultExecutionOrder(10)]
public class ArmDrive : MonoBehaviour
{
    [Header("Walk swing (deg)")]
    // Shoulder travel at |phase| = 1. Human walking swing is 20-30 deg.
    //
    // WALK CYCLE 9 (20.09, Yury's second look at the same burn_walk video):
    // «он слишком сильно машет руками». Cycle 8 took the tremor out of the rate;
    // the strength was still about twice a human's, so the amplitude came down
    // from 34 to 22.
    //
    // Measured on the cycle-8 build (the run in the tree, burn_walk.csv,
    // 2.0-4.2 s, stepWalkActive = 1) before this change: the emitted shoulder
    // travel at 34 was L 49.5 / R 56.8 deg, and the delivered joint followed it
    // at L 59.9 / R 50.6 deg of slow (0.2 s) p2p with 124 / 94 deg/s mean speed.
    // The sides are not equal because the phase excursion is not: the right
    // phase runs 1.56 p2p against the left 1.46 (the mix ratios plus the +6 dB
    // right-side peak of the cycle-8 conditioner), and the delivered tracks this
    // knob at about 1.8x on the left and 1.5x on the right. 22 therefore puts
    // the next run's delivered near the middle of the acceptance band
    // (25-55 deg) while the target stays under its 45 deg cap even if a run's
    // phase saturates the +-1 clamp the way the 19:11 baseline did (2 x 22 = 44).
    // The change is this scalar alone: the phase, the per-side mix, the elbow
    // amplitude and cycle 8's filter/slew chain are untouched, so the pairing
    // and the rate outcomes cannot move with it, and a stand (walkBlend = 0)
    // never sees it at all. `-armSwing 34` restores the old amplitude for an A/B.
    public float walkShoulderSwing = 22f;
    // Elbow: mean flexion plus the swing around it, degrees from elbowBaseAngle.
    // W57 raised the mean 12 -> 16 (the pair's visible half: the elbow's
    // straightest moment of the cycle (R / L) goes 0.8 / 10.7 deg -> 4.8 / 12.8 deg
    // measured), the walk cycle 57 block below carries the why.
    public float walkElbowMid = 16f;
    public float walkElbowAmp = 16f;
    // WALK CYCLE 57 — THE ELBOW'S OWN WALK CYCLE (Yury's eye on the 23.09 take:
    // «руки почти не сгибаются в локтях»). The c55 instrument named the cause:
    // the elbows rode the SAME `phaseNow` as the shoulders, so corr(elbow, own
    // shoulder) read +0.998 / +1.000 at slope 0.73 — the arm opened and closed
    // as ONE rigid pendulum, and an amp-only rescale cannot touch that
    // correlation (c55 §1d / §4 (0a)). This is the elbow's own phase: a
    // rate-proportional LEAD on the same-side swing the elbow and the shoulder
    // share. At the swing's own period (measured 1.57 s, the stride) 0.06 s is
    // ~13 deg of advance, so the flexion peaks on the forward drive instead of
    // sitting on the shoulder's zero-lag line. The pair's mean walked up with
    // it: mid 12 -> 16, the visible half of the fix — the elbow's straightest
    // moment of the cycle (R / L) goes 0.8 / 10.7 deg -> 4.8 / 12.8 deg measured — while
    // the amp could NOT move (every amp probe above 16 falls; see below).
    // 0 and 12 restore the pre-cycle expression bit-for-bit (c57: the
    // -armElbowLead 0 row reproduced c6w56_gene531_walk 744f0b45 byte for
    // byte). Measured on the accepted line, window 5.4-28.25 s (the c57 sweep
    // row c6w57m2, which the landed default is byte-equal to): corr vs own
    // shoulder +0.998/+1.000 -> +0.971/+0.973, flexion spans 39.9/61.2 ->
    // 43.4/62.0 deg, the row's place kept (40.0049 s, 16.87 m, 45 swaps,
    // fell False). NOTE the line's own sensitivity, measured in the same
    // sweep: this walk's trajectory is a deterministic re-roll for ANY arm
    // change (leads 0.03 / 0.075+ s, an amp of 16.5, all fall), so these
    // defaults are the measured survivor — see walk-cycle57's probe table
    // before retuning them.
    public float walkElbowLeadSeconds = 0.06f;
    // WALK CYCLE 65 - THE ARMS' BEND (the eye's item; both reads name it first:
    // the arms do not bend / the hands freeze). The drive below is a straight line in
    // the phase and the phase's own overshoot (the right side runs to +-1.8) makes both
    // ends of that line read wrong on the 5 fps take, measured on the accepted row:
    // the back swing's flexion falls to 6.8-18.4 deg (the joint reaches 4.6 and locks -
    // the arm reads as a straight stick), the front swing's rises to 66.4 while the
    // shoulder is at its own forward extreme, putting the forearm 78-107 deg off
    // vertical - horizontal in front of the body. These three fields SOFT-BAND the
    // drive's ABSOLUTE flexion (degrees from the straight arm) into [floor, ceil]:
    // smooth saturation (softplus form, C1, asymptotic to the edges), so the light
    // bend the eye asks for stays while both defects go. A hard clamp is not an
    // option on this signal (walk cycle 8's conditioner exists because clamp slams
    // shook the swing). 0/0 = OFF keeps the pre-cycle expression bit for bit (the
    // c65 control row).
    public float walkElbowFloorDeg = 30f;
    public float walkElbowCeilDeg = 52f;
    public float walkElbowBandKneeDeg = 6f;
    // C65 LANDED (24.09): the shipped band = 30 / 52 / knee 6 - the stronger of the TWO
    // surviving bands the round's sweep found (the other: 26/50; 24/46, 26/46, 26/48, 28/48,
    // 30/50, 30/54, 32/54 all fall - the line's margin is patchy, c57's lesson again).
    // Measured on the landed row (`c65post_walk`, 40.0049 s / 17.5344 m / 43 swaps / fell False,
    // the record's census kept, lift clause held): the joint's flexion band L 24.2-51.5 /
    // R 26.7-51.9 deg against the record's L 12.2-56.3 / R 4.6-67.3 - the back-swing lockout
    // (the "straight stick") and the front-swing horizontal forearm both gone; corr(elbow, own
    // shoulder) +0.974/+0.975 -> +0.962/+0.962. The round: Trials/Runs/walk-cycle65-*.md.
    // -armElbowFloor 0 -armElbowCeil 0 restores the pre-cycle expression bit for bit
    // (c65post0_walk = 38ac04aa = the c62 record row, re-earned on the final build).
    // Phase mix. crossWeight > ipsWeight is the point: the shoulder is dominated
    // by the OPPOSITE thigh (counter phase, correlation below -0.5), while the
    // same-side term keeps the two arms in counter phase with each other. The
    // ratio is a real trade-off, not a taste: in a window where the two thights
    // move together (corr(hipL,hipR) +0.9) "shoulder against the opposite thigh"
    // and "the two shoulders against each other" cannot both be strong, and the
    // measured optimum for BOTH being negative is cross/ips = 2 (t = 2).
    public float crossWeight = 1f;
    public float ipsWeight = 0.5f;
    // The right shoulder's own cross weight, i.e. the ratio is per side
    // (crossWeight/ipsWeight = 2 on the left, this one on the right). Rewritten
    // 19.09: the citations this block used to carry (a p_arms50 pair
    // "-0.658 / -0.418" and corr(dL,hipR) +0.36 vs corr(dR,hipL) +0.56) do NOT
    // reproduce from the runs they named, so they are gone rather than patched
    // (the audit is Trials/Runs/programmer-cycle-1909-1900-arm-default-claim.md and
    // -2010-arm-build-identity.md). What does reproduce, re-read this cycle with
    // `Tools/arm-pairing-check.py` on the arms5 build (fingerprint 0fd4f288), the
    // walk's own measured shoulders against the thighs, "measured" = the joint
    // columns, "drive" = the drive's own targets:
    //
    //     window 5.00-6.63 s (accepted, n = 327)   window 5.00-7.30 s
    //     p_arms81  per-side 1.5:  -0.848/-0.749 measured, -0.868/-0.866 drive
    //                                              -0.696/-0.628
    //     p_arms75  one ratio 1.0: -0.842/-0.610 measured, -0.868/-0.732 drive
    //                                              -0.657/-0.413  <- fails here
    //
    // So ONE shared ratio holds BOTH acceptance pairings in the accepted window
    // (the earlier "with ONE ratio the two pairings cannot both go below -0.5" is
    // not in the record), and breaks the right one in the wider window. The
    // default therefore stands only because the accepted evidence was measured on
    // it -- an A/B on the current build (`-armCrossR 1` against the default, two
    // runs) is the measurement this field still owes, and it is Hermes' series.
    public float crossWeightR = 1.5f;
    // +1: left shoulder against the right thigh (the acceptance pattern).
    // -1 flips to the same-side pairing, for a measurement.
    public float swingSign = 1f;
    public float walkRampSeconds = 0.30f;
    // Hip joint speed that counts as "the legs are working a gait".
    public float walkHipSpeedDegPerSec = 25f;
    public float walkHoldSeconds = 0.50f;
    // Running mean of each hip angle (the swing rides the oscillation, not the
    // fold) and the shared scale the deviation is divided by.
    public float hipStatsTau = 0.6f;
    public float minHipVarianceDeg2 = 4f;
    // Tighter tracking while walking: the recorded column must follow the drive.
    public float walkPGainScale = 4f;
    public float walkDriveSlewPerSecond = 60f;

    // ── walk swing conditioner (ARMS cycle, walk cycle 8, 20.09) ──
    // The emitted swing must read as a calm pendulum. Yury, on the burn_walk
    // video: «мне кажется человек слишком активно трясет руками». Measured on
    // that baseline (fp c98cdc70, 2.0-4.2 s): the emitted shoulder targets move
    // up to 9.9 / 3.4 deg in one 5 ms frame (mean speed 96 / 125 deg/s) with
    // 4.6 / 5.0 gated reversals per second (10-frame average, swings under
    // 1 deg ignored), against a natural pendulum of ~2 reversals/s. The shake is
    // made ABOVE the muscles: the phase is the z-scored hip deviation, and it
    // carries two artifacts. A CLAMP SLAM: the clamp at +-1 pins the target at
    // +-34 deg for up to 0.4 s and then steps 30 deg in 40 ms. And, on the
    // right side, a mid-band z-score burst: the right thigh's own deviation
    // falls to ~3 deg RMS while still carrying 1-2 deg of jitter, so the
    // quotient oscillates +-10 deg at ~3 Hz. This conditioner is the band limit
    // those artifacts need. It is per side because the two sides carry
    // different duties:
    //   LEFT holds the acceptance pairing (corr(shoulderL, opposite thigh) at or
    //   below -0.5, -0.68 measured on the baseline). A 2nd-order Butterworth at
    //   1.5 Hz keeps it (-0.863 offline) because its group delay at the ~1 Hz
    //   stride is small; a 4th-order at 1.2 Hz would cost it -0.48 and fail.
    //   RIGHT carries the burst and no pairing gate (its -0.12 is a separate
    //   open item). It takes a 4th-order Butterworth at 1.2 Hz to put the burst
    //   under the 1 deg threshold, and the peaking EQ at the stride frequency
    //   gives the swing back the steep filter costs (target p2p 23.3 -> 38.1
    //   deg, against the baseline's own 36.7).
    // Offline pass over the baseline CSV (2.0-4.2 s, all four emitted channels):
    // per-frame steps 1.46 / 0.72 / 0.69 / 0.34 deg (gates 1.75 / 1.75 / 1.0 /
    // 1.0), gated reversals 1.36 / 0.91 / 1.36 / 0.91 per second (gate 2.0),
    // target p2p 68.1 / 38.1 / 32.1 / 17.9 deg, corr(targetL, opposite thigh)
    // -0.863 (gate -0.5). The slew limits are a hard rate ceiling on the signal
    // the muscles receive, so a spikier run cannot re-open the gate: 330 deg/s
    // = 1.65 deg per 5 ms frame (shoulder gate 1.75), 190 deg/s = 0.95 (elbow
    // gate 1.0). They bite in 15 / 8 frames of 721 on the baseline.
    public float walkSwingLowPassLHz = 1.5f;
    public float walkSwingLowPassRHz = 1.2f;
    public float walkSwingPeakHz = 1.05f;
    public float walkSwingPeakDb = 6f;
    public float walkSwingPeakQ = 1f;
    public float walkShoulderSlewPerSecond = 330f;
    public float walkElbowSlewPerSecond = 190f;
    // 1 = the RIGHT swing chain is conditioned exactly like the LEFT: one 2nd-order low-pass at
    // walkSwingLowPassLHz, no cascade and no peaking, and the right mix uses crossWeight
    // (-armSwingLpR / -armSwingPeakDb / -armCrossR are then ignored). 0 = the per-side chain.
    public float armSwingMirror = 0f;

    [Header("Fall press")]
    public float pushEnabled = 1f;
    public float fallTiltDeg = 45f;
    // WALK CYCLE 45 (the walk lane's fall guard, -poseFallGuard): while the pose
    // drive's own state says the body is going over it publishes its reach here,
    // and the plant point below is then taken ON THE SLAB that far ahead of the
    // shoulder. 0 = off - the value every pre-existing command line leaves, and
    // the value that keeps every line of this file's press machinery
    // byte-identical. Signed, m: +x = the latched point sits ahead of the
    // shoulder.
    public float fallGuardReach = 0f;
    // CYCLE 100 - THE GET-UP'S OWN PLANT REACH (the owner's hand-plant addendum):
    // the palms must brace AHEAD of the chest, in the shoulder zone, never at the
    // navel. SIGNED metres: +x = the latched point sits ahead of the shoulder.
    // Same slab geometry as the guard's reach; ONE difference: the stroke stays
    // (this is the press, not the catch). 0 = off - the landed rule, every
    // pre-existing command line.
    public float getUpPlantReach = 0f;
    // The guard's own reach is 0.2-0.6 s of arm travel: the landed press slew (60
    // deg/s) is a rate for a body already on the slab, not for a catch.
    private const float FallGuardSlewDegPerSecond = 300f;
    // The press is a closed chain, and it only works if the palm is a FIXED
    // point: the hand target is latched in the world when the palm lands and is
    // not moved again until the stroke ends. Measured p_arms20 with a target
    // that followed the shoulder: 191 strokes per side, 2.7 deg of elbow
    // extension, the arm walked its hand along the slab and the body never rose.
    // The stroke runs Press (elbow extends, shoulder aimed at the latched
    // point) -> Hold -> Lift (palm unloaded, arm folds again) -> Plant (reach
    // the next latched point, the stroke starts on real contact).
    // Plant geometry: the palm lands on the slab at the SMALLEST lateral offset
    // the arm can manage — the closer the palm is to under the shoulder, the more
    // of the elbow extension goes into lifting the body instead of shoving it
    // sideways. The offset is the horizontal leg of the shortest chord the folded
    // arm can make: with the elbow at its flexion stop the arm spans
    // sqrt(L1^2+L2^2-2 L1 L2 cos interior), and anything shorter is unreachable.
    // Measured p_arms21 with a fixed 0.30 m offset: the push left the slab at
    // 57 deg off vertical, the palm slipped and the shoulder rose 70 mm.
    public float plantSideMax = 0.35f;
    // Extra chord over the arm's own shortest usable one that a latched point is
    // kept at, metres. The plant is a CHORD question, not a posture question:
    // measured p_arms50 (plantLateralShare 0), a lying shoulder 0.09-0.12 m over
    // the slab latched the point straight under itself, 0.11 m away against an arm
    // whose shortest usable chord is 0.218 m. The solve clamped, the hand settled
    // 0.32 m away from its own palm, the elbow "extended" 60 deg into the air and
    // the shoulder rose 0.03 m. NEGATIVE values are legal and mean "the chord may
    // be shorter than the arm's own minimum", i.e. the palm goes straight under
    // the shoulder and the solve presses it INTO the slab by the difference --
    // that is the load a press wants. Measured 19.09 on the -165 elbow stop
    // (p_arms70 = the old rig, press on, 0.03): a stroke opened the elbow 40-80
    // deg and moved the shoulder pivot 0.005 m while shoving the body 2.08 m.
    public float plantReachMargin = 0.03f;
    // ARM cycle 13. The chord rule above computes the latch from the posture on
    // the frame the latch happens -- and that frame is the SAGGED one, because a
    // new point is taken when a stroke ended and the palm was unloaded. Measured
    // on p_arms70 (build 559e2e50, press on against its own press-off control
    // p_arms71): the palm's own chord grew 0.267 -> 0.506 m inside one stroke
    // while the shoulder pivot went DOWN 0.09 m and the body travelled
    // comTravelX 3.58 m against the control's 1.63 m -- the elbow's extension
    // walked the palm along the slab and lifted nothing (pelvisY mean -1.838
    // against the control's -1.840, its best height LOWER than the control's:
    // -1.654 against -1.619). 1 = the latch is computed from the highest pivot
    // this side has held WITH the palm on the slab during the stroke it just
    // finished, so the drop is the posture the side has proven, and the offset
    // sqrt(reach^2 - drop^2) collapses to 0 (palm straight under the shoulder,
    // push line vertical) as soon as that drop reaches the arm's own reach.
    // 0 = the stored behaviour (the sagged frame), the same-build control.
    public float plantHighLatch = 1f;
    // How much of the latched point's lateral offset is filled by the chord rule.
    // 0 = the palm goes under the shoulder (the press posture); 1 = the former
    // lateral-fill rule, kept so the two can be compared on one build.
    public float plantLateralShare = 0f;
    // Interior angle the plant starts the elbow at: the room the stroke has to
    // extend into, 65 deg = about 115 deg of flexion left to spend, and the
    // elbow target it asks for (-115) sits well inside the -150..0 hinge.
    // Measured p_arms34 with the former "shortest chord the arm can make" rule:
    // a lying shoulder 0.11 m over the slab latched a 0.11 m chord against an
    // arm whose minimum chord is 0.218, so the solve asked for an 18 deg
    // interior angle (the stop is 30), the elbow pinned on the stop and the
    // "stroke" threw the hand 0.66 m into the air while counting 126 deg of
    // "extension" -- extension of a joint that never had the palm loaded.
    // Margin in degrees of elbow interior angle kept above the joint's own stop
    // when a plant point is chosen. The stroke starts AT this fold, so the margin
    // is the elbow torque the stroke has at t=0: too small and the press begins
    // against the stop (p_arms34, 8 deg margin: plantFold -142, 1.6 mm of rise).
    public float plantStopMarginDeg = 25f;
    public float plantFoldMarginDeg = 8f;
    public float plantReachSeconds = 1.2f;
    // Palm centre this far below the slab top: the collider is pressed onto the
    // ground rather than resting on it, which is what makes IsTouchingLayers
    // fire (a geometric slop test does not).
    public float handGroundSink = 0.02f;
    // The stroke drives the latched palm point this far INTO the slab. A palm
    // that is merely asked to stay where it is needs no torque; one that is
    // asked to go through the floor loads the chain, and the block is then the
    // ground, so the only thing that can give is the shoulder: the body rises.
    // Measured p_arms34 with an unloaded latched point: the elbow opened 125 deg
    // in 0.11 s, the hand rose 0.66 m off the slab and the shoulder rose 1.6 mm.
    public float pressDepth = 0.04f;
    // ROUND 15 (the plant-phase ejection price): cap on how deep the stroke may
    // drive the palm's latched point. 0 = off (the slab is the only limit). > 0 =
    // the target never goes more than this far below the palm's own bottom face:
    // a load-bearing palm then cannot sink through a thin deck plate and be popped
    // out by the deck's own upswing (measured sitB: the palm sat ~4 cm deep when
    // the solver threw the body at t 25.505).
    public float pressSinkCap = 0f;
    // THE SELF-ORIENTATION BRANCH (round 17, addendum 8). While the body is on its BACK
    // (PoseOrientation.LyingBack) the press must not keep solving an arm onto a slab
    // point the prone way - on a supine body that solve drives the shoulder "forward",
    // which in the world points the arm straight off the deck (the owner's evidence
    // frame: a man on his back, arms driven forward/up). The arms rest along the body
    // instead; a fresh latch is taken when the pose recovers. The component exists only
    // when -poseOrient armed it; null = every legacy path.
    private PoseOrientation orient;
    private bool orientTried;
    private readonly bool[] orientRestOn = new bool[2];
    public bool orientLog = true;
    public int OrientRestEntries { get; private set; }
    // Stroke mechanism. 1 = the latched point itself is driven deeper into the
    // slab and BOTH joints keep solving onto it, so the hand stays on its own
    // palm and the giving link is the shoulder; 0 = the former elbow-angle ramp,
    // which measured p_arms40 let the hand walk 0.39 m past the palm it was
    // supposed to press on.
    public float pressStrokeMode = 1f;
    // Extra depth the latched point gains over one stroke, m, and the rate it
    // gains it at, m/s. The chord is capped by the arm's own length, so this is
    // the room a stroke has to press into.
    public float pressDepthExtra = 0.14f;
    public float pressDepthRate = 0.09f;
    public float pressElbowTarget = -4f;
    // Rate limit on the stroke's elbow goal, deg/s (0 = off). Was off by default
    // on the assumption that "with the palm loaded the slab itself limits the
    // rate" -- measured p_arms34/36, it does not: an arm whose shoulder can rotate
    // freely just throws the HAND, so the elbow snapped 110 deg in 0.1 s (1000
    // deg/s) and the hand left the slab it had been gripping the step before. With
    // the goal ramping at this rate the extension can only be paid for by the
    // shoulder moving, which is what a press is.
    public float pressStrokeDegPerSecond = 60f;
    // The stroke has done its work when the elbow stops extending this long.
    public float pressStallSeconds = 0.5f;
    // A stroke is latched for at least this long, and contact has to be lost for
    // pressLostContactSeconds before it may be abandoned: the churn of p_arms20
    // (0.18 s per "stroke") was a contact flicker, not a lost slab.
    public float pressMinStrokeSeconds = 0.8f;
    public float pressMaxSeconds = 3.0f;
    public float pressLostContactSeconds = 0.15f;
    public float pressReengageDeg = 12f;
    public float pressReengageCooldownSeconds = 0.4f;
    public float liftHoldSeconds = 0.30f;
    // Height the Lift phase carries the hand at, metres above the latched palm.
    // A lift that solves to the plant point itself is a horizontal slide ALONG
    // the slab, which the ground blocks: measured p_arms52, the hand never left
    // the spot under its own shoulder, the elbow folded onto its own stop (-150)
    // and the strokes that followed delivered 1-3 deg of "extension" with the palm
    // 0.20 m out of reach. Carrying the hand clear of the slab is the right motion
    // but the wrong default HERE, and that is measured too: p_arms53/54 with 0.08
    // m kept the palm on the slab 89-92 % of frames yet the chest ended flat on
    // the ground (pivot -1.89, |tilt| 90, pelvis -1.860), against p_arms55/57 with
    // 0 where the chest ended PROPPED (pivot -1.60, |tilt| 58, pelvis -1.73). The
    // hand leaving the slab takes the only support the chest has. 0 = the hand
    // stays planted through the lift.
    public float liftClearanceMeters = 0f;
    // Shoulder bias added to the aim during the stroke, degrees.
    public float pressShoulderBias = 0f;
    // A stroke ends when the elbow is within this of the extension target.
    public float pressDoneMarginDeg = 6f;
    // Aim gain of the shoulder onto the latched palm (1 = point at it each step).
    public float pressAimGain = 1f;
    public float pressTorqueScale = 4f;
    // ── WALK CYCLE 29: THE AFTERFALL PRESS FLOOR (cross-lane edit, sanctioned
    //    and NAMED in the walk round c29's report; the arms stream is paused
    //    since 20.09) ──
    // Report 030 named the press's stroke amplitude (extMax) the afterfall
    // floor's one producer. c29 measured it: the amplitude is a CORRELATE. The
    // floor tracks the press's DELIVERED FORCE CEILING - sensorComXMax 2.62..2.89
    // at scale 4, 3.10 at 5, 4.03..4.25 at 6 - while a target-side amplitude
    // drive (built, tabbed, and reverted this round: extMax 136.7 with the floor
    // still at 2.89, and 2.69 when tuned at scale 6) does not buy it and steals
    // from the stroke's delivery. So while the press owns the arms - `pressing`
    // is down-gated, the walk never presses - the muscles work at no less than
    // this scale. Outside the press the ceilings are restored exactly, as before.
    public float pressAfterfallTorqueScale = 6f;
    // ARM cycle 11. pressTorqueScale raises the shoulder/elbow CEILINGS while the
    // body is down; the PD's damper must not follow that boost. ArmDrive.Drive
    // computes signal = raw / (1 + damping) with damping = dGain * torque *
    // Rad2Deg / speedRef * dt / Ieff, so a 4x ceiling was also a 4x damper and the
    // press throttled its own activation: on the six stored runs of build
    // 41fd907f the mean |actElbow| inside a press is 0.043-0.211 and no stored
    // press anywhere saturates the joint (116 side-samples, 0.03-0.68), while the
    // error against its own aim averages 14.10 deg on the left of p_arms70 -- an
    // error that asks for a full signal. The single-switch pair on build 41fd907f
    // says it is the ceiling: p_arms57 (-armLiftClear 0 -armPushScale 4) act
    // 0.117/0.137, palm contact 0.544/0.660; p_arms56 (+ -armPushScale 6) act
    // 0.082/0.050, contact 0.482/0.290 -- a HIGHER ceiling pressed LESS. 1 = the
    // damper uses the joint's base ceiling (shoulder 40, elbow 30 N.m), which is
    // what the walk has always had at scale 1; 0 = the former behaviour, kept as
    // the control on the same build.
    public float pressDamperFromBase = 1f;
    // ARM cycle 12. The press loads the palm through the ELBOW, whose ceiling the
    // press raises to elbowMuscleTorque x pressTorqueScale = 120 N.m, while the
    // pair that actually holds the contact -- the HAND's two muscles, 10 N.m --
    // is driven by the balance controller and never sees the boost. The grip is
    // measurably lost exactly when the press arrives, on one build, one switch
    // apart (`Tools/arm-wrist-hold-check.py`, all numbers from the stored runs):
    //
    //     p_arms70 (press ON)   armHandContactFraction 0.9258/0.7503
    //     p_arms71 (press OFF)  armHandContactFraction 0.9755/0.9589   <- control
    //
    // and the palm sits +0.0408/+0.0506 m ABOVE the point the drive latched for
    // it while the press holds (its own CSV). 1 = the press owns the hand too:
    // the wrist is driven onto its own angle at the latch with the same stable PD
    // and the same base-ceiling damper as the shoulder and the elbow, and the
    // hand's ceiling is scaled with the press the way theirs is. 0 = the wrist is
    // left to the balance controller exactly as before, the control on the same
    // build.
    public float pressWristHold = 1f;
    public float pressPGainScale = 2f;
    public float pressDriveSlewPerSecond = 60f;
    // Same rate limit for the ELBOW target, deg/s. The shoulder had one from
    // p_arms38 and the elbow did not, and that asymmetry is measurable: on
    // p_arms60 the down-window elbow TARGET moved at 446-491 deg/s while the
    // shoulder target moved at 58-60 (its own cap) -- the drive was asking the
    // elbow for a step and the muscle spent the stroke catching up.
    public float pressElbowSlewPerSecond = 90f;
    // Real contact for the press, plus a small band so a palm 1-2 mm above the
    // slab still counts as planted.
    public float handNearGroundSlop = 0.04f;
    // Keep the latched palm pressed while the hand's own orientation changes. The
    // hand is 0.16 long, so a palm latched upright (half-height 0.08) that lands
    // FLAT (0.03) hovers 50 mm over the slab and never makes contact -- measured
    // p_arms43, the palm centre sat 0.02 m over the slab and only 46-68 % of the
    // press samples carried a real contact. The correction moves the target in Y
    // only (that axis IS "how hard the palm is pressed"), never in X, so it cannot
    // walk the palm sideways. 0 = the latched world point is frozen as before.
    //
    // MEASURED 19.09 at full size (`_arms-phases2.py` on the arms5 build,
    // fingerprint 0fd4f288, ground window 7.88-40.00 s, 6426 frames, contact read
    // from the recorded `handContactL/R` per phase):
    //
    //     label     phase share L/R      contact inside the phase L/R
    //     p_arms81  plant 38.6/34.5 %    plant  0/0 %   <- the defect
    //               press 35.2/36.4 %    press 64/73 %
    //               hold  15.7/18.6 %    hold  57/66 %
    //               lift  10.4/10.4 %    lift  45/55 %
    //
    // i.e. with the Y frozen at the latch the palm misses the slab ENTIRELY
    // (0 % on both sides) for more than a third of the fall, and that dead phase
    // is why only 2 of 10 (L) and 4 of 11 (R) strokes ever reached the 20 deg the
    // acceptance asks for: a stroke cannot start or extend while the palm is in
    // the air, and `plantReachSeconds` re-latches a fresh point that misses the
    // same way. The latch is taken where the hand STANDs on end (half-height
    // 0.08) and the palm arrives FLAT (0.03), so the frozen target sits about
    // 50 mm high -- the p_arms43 error, not a new one. The palm's own thickness is
    // the only thing this follows; the lateral offset stays latched. Default ON.
    // `-armPalmLive 0` restores the frozen point, `-armPalmLive 0..1` lerps.
    public float pressPalmLiveDepth = 1f;
    // ARMS cycle 14. A press-up is a TWO-PALM move, and the drive used to give one
    // palm up on a clock. Measured on p_arms81 (the newest arms build, 40 s, the
    // press-ON run) over the fallen window: the pelvis rises and falls back 18 times
    // by >= 0.05 m (drops up to 0.157 m), and in 16 of those 18 fall-backs BOTH palms
    // are loaded for 0.0-17.1 % of the drop (13 of them exactly 0.0 %; the drop is
    // carried by one palm in 11 of the 18). One side is in Plant/Lift at the top or
    // the bottom of 17 of the 18. The rise that precedes them (29.5-30.4 s, pivot
    // -1.789 -> -1.600 m) runs with BOTH sides in Press/Hold. The run's own split is
    // both 12.2 %, one 56.3 %, none 31.5 %, against the press-OFF control's both
    // 96.4 %. So while this side's palm is STILL on the slab, the Hold must not give
    // the point up to plant a fresh one: it re-engages the stroke from the point it
    // holds, and the two palms stay loaded together. Only a palm that has really left
    // the slab (lostContactTime >= pressLostContactSeconds) re-plants as before.
    // Default ON; `-armTwoPalm 0` is the stored behaviour on the same build.
    public float pressTwoPalm = 1f;
    public float pressRequestHoldSeconds = 0.25f;

    // ─── THE STRIKE (the punch) — punch cycle p1 ───────────────────────────
    // The arm strike is the LAYER the owner ordered (25.09: «Удар рукой»): a
    // wind-up, an extension and a return along the arm chain (shoulder, elbow,
    // wrist) on ONE arm. It is a layer ABOVE the always-on base, and the two
    // properties that make it one are structural, not a promise:
    //  * it writes ONLY the striking arm's muscles, so the trunk (the
    //    vestibular law), the pelvis law, the ankle/CoP loop and both legs keep
    //    their own writers and the equilibrium stays with the background - the
    //    strike cannot freeze the stance;
    //  * it is delivered HERE, in ArmDrive, which BalanceController's
    //    `poseDrive.Tick(comOffset); return;` cannot skip: this component is
    //    execution order 10, after BalanceController (order 0) - the defect that
    //    killed the crouch/leans (their delivery sat below that early return)
    //    has no branch to hide in on this path.
    // The request comes from MotionIntent.punch (the F key in Play, the trial's
    // -punch flag, Tools/force-punch.flag): rising edge fires, a held request
    // re-fires every punchIntervalSeconds. While the body is DOWN the press owns
    // the arms (the fall is the base's own state) and no strike runs.
    //
    // The angles are targets for the SAME stable PD every other arm target uses
    // (no new torque law): base -> back (the cook) -> hit (the extension) ->
    // base, smoothstepped inside each phase. Sign convention of this rig:
    // negative = flexion = limb forward, so the hit is a NEGATIVE shoulder.
    // 0 = off: no strike can fire (R2 — with punch 0 every expression below is
    // skipped and the emitted targets are the landed ones bit for bit).
    public float punchWindSeconds = 0.14f;
    public float punchExtendSeconds = 0.12f;
    public float punchReturnSeconds = 0.24f;
    // Re-fire clock while the request is held (the stroke is 0.50 s, so a held
    // key punches about once a second).
    public float punchIntervalSeconds = 0.90f;
    // Shoulder: the cook pulls the arm BACK (+), the hit drives it forward: at
    // -88 the upper arm is level with the ground, the rig's joint range is
    // -160..90 (Human.cs).
    public float punchShoulderBackDeg = 18f;
    public float punchShoulderHitDeg = -88f;
    // Elbow: folded on the cook (-58), open at the hit (-6; the press's own
    // full-extension order is -4). Elbow range is -165..0.
    public float punchElbowBackDeg = -58f;
    public float punchElbowHitDeg = -6f;
    // Wrist, the chain's last link: cocked on the cook (+ = extended), driven
    // through the hit. Range -60..60. ArmDrive takes this joint over on the
    // striking side for the stroke only (the press does the same while a press
    // owns a side).
    public float punchWristBackDeg = 14f;
    public float punchWristHitDeg = -8f;
    // Which arm strikes: +1 the RIGHT (the lead), -1 the left. One arm per
    // stroke; the other keeps whatever the base was already asking it.
    public float punchSide = 1f;
    // Authority for the extension: a 0.9 s walk swing needs none, a strike
    // does. Same lever class as pressTorqueScale/walkPGainScale, and the damper
    // stays tuned at the joint's BASE ceiling (pressDamperFromBase), so the
    // boost buys speed, never damping.
    public float punchTorqueScale = 3f;
    public float punchPGainScale = 2f;
    // Rate cap on the emitted strike target, degrees per second: the wind-up
    // starts from the base pose and the arm may be mid-swing, so the command
    // must not step. 1200 leaves the 0.12 s extension (about 880 deg/s) free.
    public float punchSlewDegPerSecond = 1200f;
    // The activation slew while the strike owns a side, degrees per second. The
    // stand's own arm drive slews at pressDriveSlewPerSecond (60/s, the quiet
    // stance) and the walk at 330/s; a strike needs the muscles to arrive with
    // the command, so its own value is a knob and not the stand's.
    public float punchDriveSlewPerSecond = 600f;
    // THE COUNTER-ARM. A strike thrown by ONE arm is outside this rig's base:
    // measured (dose ladder 25.09) the swing puts a horizontal impulse through
    // the soles - the ankle loop pushes the body forward to answer the trunk's
    // pitch, the sole skids, and the stand drifts forward 11-15 mm PER STROKE
    // until the CoM leaves the support (the gentlest shape tried, -35 deg, fell
    // after five strokes; the full 90 deg shape fell on the first one). Real
    // humans throw the other arm BACK: the two arms then carry opposite angular
    // momentum, the trunk gets no net impulse and the ground has nothing to
    // answer. This mirror does exactly that - the non-striking shoulder takes
    // the NEGATIVE of the striking shoulder's travel from the base pose, with
    // the same gains and ceilings - and it is a share, because an arm's mass
    // need not be cancelled 1:1 to be cancelled; `-punchCounter 0` restores the
    // one-arm strike of the dose ladder bit for bit.
    public float punchCounterShare = 1f;
    // The counter-arm mirror's own travel cap, degrees from the base pose. The
    // mirror is the negative of the striking shoulder's travel, and at share 1
    // with a -88 deg strike that asks for +88 - inside the joint's own +90 stop
    // but ON it: measured (c1, 25.09) the counter arm reached +90, slammed the
    // stop, and the shock put BOTH ankles on their rails (appTorqueAnkleL -52 ->
    // -65, fzTotal -> 0) - the stand hopped and fell on the second stroke. The
    // cap keeps the counter arm off its stop; the momentum it can carry is then
    // what the joint can actually hold, not what the mirror would like.
    public float punchCounterMaxDeg = 60f;

    // --- CYCLE 112 - THE GET-UP MAT SLAP (the owner's slap-impulse addendum) ---
    // A fast DOWNWARD strike of both palms onto the slab, fired at a get-up
    // attempt entry, so the contact impulse can peel the heavy torso. Same
    // layer rules as the punch: it writes targets only, for the stroke's own
    // window only, and 0 (default) leaves every emitted target bit for bit.
    // The strike is served by the press's own world-point solve (SolveArm):
    // the palms rise getUpSlapLiftMeters off their contact point, then drive
    // back down eased-in, so the palm is at its fastest when it reaches the
    // slab. `-getUpSlap <m>` arms it with that lift (0 = off). CYCLE 113: the
    // landed default is 0.25 and every fire is gated by the settle rule below
    // (the c112 wrinkle fix: no mid-fall flails).
    public float getUpSlapLiftMeters = 0.25f;
    public float getUpSlapLiftSeconds = 0.14f;
    public float getUpSlapDriveSeconds = 0.09f;
    public float getUpSlapSettleSeconds = 0.12f;
    public float getUpSlapDriveSlewPerSecond = 600f;
    public float getUpSlapPGainScale = 2f;
    // CYCLE 113 - THE SETTLED-BODY GATE (the c112 named-behind: "fire only
    // from a settled down body"). The c112 fire lands on the attempt
    // counter's rising edge, and the FIRST attempt of a fall lands while the
    // body is still toppling - a slap fired mid-fall reads as a flail, not a
    // strike. Armed, the fire is refused unless the body has been QUIET for
    // getUpSlapQuietSeconds at the edge: |torso angular velocity| <=
    // getUpSlapSettleDeg (deg/s) and |pelvis vertical velocity| <=
    // getUpSlapSettleMps (m/s); a ceiling left at 0 drops that half. Both
    // ceilings 0 reproduces the c112 fire rule bit for bit (reachable with
    // explicit zeros); the landed 130/0.15 pair is what cycle 113 shipped.
    public float getUpSlapSettleDeg = 130f;
    public float getUpSlapSettleMps = 0.15f;
    public float getUpSlapQuietSeconds = 0.25f;
    private float slapQuietAge;
    private float slapClock = -1f;
    private int slapAttemptsSeen;
    private float[] slapAnchorX = new float[2];
    private float[] slapAnchorY = new float[2];
    private float[] slapLiftY = new float[2];
    private float[] slapPrevY = new float[2];
    private bool[] slapPrevYSeen = new bool[2];
    private bool slapLanded;

    // Read-only state, for the recorder and the get-up.
    public bool HandContactL { get; private set; }
    public bool HandContactR { get; private set; }
    public bool HandNearGroundL { get; private set; }
    public bool HandNearGroundR { get; private set; }
    public float ArmPhaseL { get; private set; }
    public float ArmPhaseR { get; private set; }
    public float WalkSwingLevel { get; private set; }
    public float PressRequest { get; private set; }
    public int PressPhaseL { get; private set; }
    public int PressPhaseR { get; private set; }
    public float ArmTargetShoulderL { get; private set; }
    public float ArmTargetShoulderR { get; private set; }
    public float ArmTargetElbowL { get; private set; }
    public float ArmTargetElbowR { get; private set; }
    public int PressStrokesL { get; private set; }
    public int PressStrokesR { get; private set; }
    // CYCLE 112: the get-up mat slap's own counters (the strike's numbers).
    public int SlapFires { get; private set; }
    public int SlapEnds { get; private set; }
    public int SlapBlocks { get; private set; }
    public float SlapPeakDownMps { get; private set; }

    // FALL REFLEX (29.09): while these are not NaN this component emits the reflex's
    // brace targets for both shoulders/elbows - the reflex writes the COMMAND here so
    // ArmDrive keeps sole ownership of the arm muscles (one writer per joint) - and the
    // press gate reads them as "the reflex owns the arms". float.NaN (the default)
    // leaves every existing take - stand, walk, ball, drop - bit for bit.
    public float reflexShoulderDeg = float.NaN;
    public float reflexElbowDeg = float.NaN;
    private bool ReflexOwnsArms { get { return !float.IsNaN(reflexShoulderDeg); } }
    // Largest elbow extension over the WHOLE RUN, degrees (the acceptance wants
    // more than 20). Run-scoped since arms cycle 10: every one of these maxima
    // used to be reset by `BeginStroke`, so the number the recorder published was
    // the LAST stroke's, and a run whose final stroke is cut off by the end of
    // the trial published 0.000 for a press that really extended -- the "strokes
    // 0.0 deg" reading on p_arms81 (its own CSV reaches 75.80 deg on that side).
    // The last stroke's own value is kept separately below (PressLastStroke*).
    public float PressStrokeExtensionL { get; private set; }
    public float PressStrokeExtensionR { get; private set; }
    // Largest shoulder extension over the whole run, degrees.
    public float PressStrokeShoulderExtL { get; private set; }
    public float PressStrokeShoulderExtR { get; private set; }
    // Largest pelvis rise measured inside one press stroke, metres: this one is
    // already run-scoped (`TrackStroke` only ever raises it, nothing resets it),
    // so cycle 9's note calling it stroke-scoped was wrong about this column.
    public float PressLiftMax { get; private set; }
    // Largest shoulder rise over the whole run, per side, metres: the palm is
    // latched on the slab, so this is the push itself, not the pelvis (which
    // only follows if the legs hold).
    public float PressShoulderLiftL { get; private set; }
    public float PressShoulderLiftR { get; private set; }
    // The last stroke's own maxima, so the run-scoped numbers above lose no
    // information: read these when the question is about one stroke, the ones
    // above when the question is what the run ever achieved.
    public float PressLastStrokeExtensionL { get; private set; }
    public float PressLastStrokeExtensionR { get; private set; }
    public float PressLastStrokeShoulderExtL { get; private set; }
    public float PressLastStrokeShoulderExtR { get; private set; }
    public float PressLastStrokeShoulderLiftL { get; private set; }
    public float PressLastStrokeShoulderLiftR { get; private set; }
    // Lateral offset of the latched palm from the shoulder pivot, metres.
    public float PressPlantOffsetL { get; private set; }
    public float PressPlantOffsetR { get; private set; }
    // The elbow angle the plant asked for, degrees: how much fold the stroke
    // starts from, i.e. the room the press has to extend into.
    public float PressPlantFoldL { get; private set; }
    public float PressPlantFoldR { get; private set; }
    // CYCLE 100 (the hand-plant read): where the latched palm's world point sits
    // FORE-AFT of its shoulder pivot, metres. >0 = the plant point is at greater
    // world x than the shoulder (ahead in these takes' facing), <0 = behind it,
    // toward the waist/navel. The hand-plant acceptance reads the prone press
    // frames from this pair.
    public float PressPlantForeL { get { return plantX[SideL] - ShoulderPivotWorld(SideL).x; } }
    public float PressPlantForeR { get { return plantX[SideR] - ShoulderPivotWorld(SideR).x; } }
    // The armed get-up plant reach command, metres (signed; 0 = off).
    public float GetUpReachCmd { get { return getUpPlantReach; } }
    // The drop and the push line the LAST latch was computed from (arms cycle 13):
    // drop = the height the latch measured the shoulder pivot at above the palm's
    // own centre, tilt = the push line's angle off vertical, degrees. The pair is
    // read against the palm-ground friction cone -- sqrt(handFriction x
    // groundFriction), both recorded per run -- because a push line outside that
    // cone cannot load the slab at all, it slides.
    public float PressPlantDropL { get; private set; }
    public float PressPlantDropR { get; private set; }
    public float PressPlantTiltL { get; private set; }
    public float PressPlantTiltR { get; private set; }
    // The hand's own hold (arms cycle 12), read-only so the hand can be measured
    // at all: no p_arms* record carries a wrist column today, and the recorder is
    // the walk worker's file. Angle and latch in degrees, activation 0..1.
    public float PressWristAngleL { get { return wrist[SideL] != null ? wrist[SideL].jointAngle : 0f; } }
    public float PressWristAngleR { get { return wrist[SideR] != null ? wrist[SideR].jointAngle : 0f; } }
    public float PressWristHoldL { get { return wristHold[SideL]; } }
    public float PressWristHoldR { get { return wristHold[SideR]; } }
    public float PressWristActL { get { return wrFlex[SideL] != null ? wrFlex[SideL].activation : 0f; } }
    public float PressWristActR { get { return wrFlex[SideR] != null ? wrFlex[SideR].activation : 0f; } }
    // Stroke re-engages taken from a palm that was still on the slab instead of a
    // fresh plant, per side (arms cycle 14). Both must be > 0 for the change to be
    // alive at all; a 0 on a fall with two palms down means the branch never fired.
    public int PressTwoPalmReengagesL { get { return twoPalmReengages[SideL]; } }
    public int PressTwoPalmReengagesR { get { return twoPalmReengages[SideR]; } }
    // Shoulder pivot height and the latched palm height, metres. The gap between
    // them is what the arm has to span, so a press that cannot reach the slab is
    // told from one that reaches it and is not loaded.
    public float PressPivotYL { get { return ShoulderPivotWorld(SideL).y; } }
    public float PressPivotYR { get { return ShoulderPivotWorld(SideR).y; } }
    public float PressPalmYL { get { return PlantTarget(SideL).y; } }
    public float PressPalmYR { get { return PlantTarget(SideR).y; } }
    // World direction the upper arm points, degrees, and the centre of the
    // direction window the shoulder can reach (rest direction) — so an aim that
    // the joint range cannot serve is a measured distance, not an opinion.
    public float PressAimHaveL { get { return UpperArmWorldDeg(SideL); } }
    public float PressAimHaveR { get { return UpperArmWorldDeg(SideR); } }
    public float PressRestL { get { return UpperArmRestDeg(SideL); } }
    public float PressRestR { get { return UpperArmRestDeg(SideR); } }
    public float PressHandContactFractionL { get; private set; }
    public float PressHandContactFractionR { get; private set; }

    // ─── THE STRIKE's read-back (punch cycle p1) ───
    // The recorder is the walk worker's file and carries no strike column (the
    // strike's own numbers are read from it: `armTargetShoulderR/ElbowR` are
    // outShoulder/outElbow - the targets THIS component emits - and
    // `shoulderAngleR/elbowAngleR` are what the joint delivered). These are the
    // strike's own state for the round's own reads and the next cycle's column.
    public float PunchRequest { get; private set; }
    public int PunchPhase { get; private set; }          // 0 idle 1 wind-up 2 extension 3 return
    public int PunchStrikes { get; private set; }        // strokes started this run
    public float PunchAge { get; private set; }          // seconds since the stroke in force started
    public float PunchTargetShoulder { get; private set; }
    public float PunchTargetElbow { get; private set; }
    public float PunchTargetWrist { get; private set; }
    // Run-scoped maxima, the press's own convention: the peak the emitted
    // shoulder target reached, the largest elbow opening, and the fastest the
    // shoulder TARGET moved (deg/s) - the strike's own rate, read against the
    // walk swing's 330 deg/s cap so "a strike, not a wave" is a number.
    public float PunchPeakShoulderDeg { get; private set; }
    public float PunchPeakElbowOpenDeg { get; private set; }
    public float PunchPeakTargetRateDegPerSec { get; private set; }
    public float PunchLastStrokeSeconds { get; private set; }
    private float punchStrokeSeconds;

    // ─── matched press window (instrumentation, arms cycle 8) ───
    // Every press number above is scoped to a STROKE, so on the press-OFF
    // control (`-armPush 0`) no stroke ever starts and all of them read 0.000 by
    // construction: the control arm of the A/B has no value at all, and the
    // claim "the push lifts the body" has never had a counterfactual to be read
    // against. These are measured over a window that starts on the DOWN EDGE
    // itself (the frame `down` turns true, which does not depend on pushEnabled)
    // and is sampled every step while the body is down, so the same columns
    // exist, over the same simulation seconds, with the press on and off on ONE
    // build. Rise and travel in one window: the press's own record says the body
    // was shoved 2.08 m along the slab, and a shove and a lift are told apart
    // only when both are read in the same window.
    public float PressWindowSeconds { get; private set; }
    // The rise columns are measured against the LOWEST point the window has seen
    // so far, not against the height on the down frame. The original definition
    // (rise above the down frame) is a dead column on every fall this rig makes:
    // the latch is |tilt| >= fallTiltDeg, which trips while the body is still
    // going over, so the reference is taken at the standing shoulder height and
    // every later sample is below it. Read off the stored CSVs of the 19.09
    // series, `armPivotY` sits at -1.0203 m on the down frame (t 7.385 s) against
    // a settled -1.81..-1.87 m, and the same definition on those CSVs gives exactly
    // 0.0000 on 7 of 7 runs (p_arms55/58/60/63/70/80/81) press ON, i.e. the p_arms83/84
    // A/B would have printed 0.0000 against 0.0000 and the falsifier would have
    // fired on an artefact. As a climb out of the window's own minimum it does
    // separate the arms: on the one single-build press pair (p_arms70 ON,
    // p_arms71 OFF, build 7ebbad89, rows identical up to the down edge) it is
    // +0.1269/+0.1262 m against +0.0871/+0.0835 m, while travel is 2.8042 m
    // against 1.0589 m. Cycle 8's semantics is kept in the comment, not in a
    // column: no stored run carries this key yet, so nothing is broken by fixing
    // the definition before its first measurement.
    public float PressWindowShoulderRiseL { get; private set; }
    public float PressWindowShoulderRiseR { get; private set; }
    public float PressWindowPelvisRise { get; private set; }
    public float PressWindowTravelX { get; private set; }
    public float PressWindowContactFractionL { get; private set; }
    public float PressWindowContactFractionR { get; private set; }

    private const int SideL = 0;
    private const int SideR = 1;
    private const int PhaseIdle = 0;
    private const int PhaseLift = 1;
    private const int PhasePress = 2;
    private const int PhaseHold = 3;
    private const int PhasePlant = 4;

    private BalanceController balance;
    private VestibularSystem vestibular;

    private readonly HingeJoint2D[] hip = new HingeJoint2D[2];
    private readonly HingeJoint2D[] shoulder = new HingeJoint2D[2];
    private readonly HingeJoint2D[] elbow = new HingeJoint2D[2];
    private readonly Muscle[] shFlex = new Muscle[2];
    private readonly Muscle[] shExt = new Muscle[2];
    private readonly Muscle[] elFlex = new Muscle[2];
    private readonly Muscle[] elExt = new Muscle[2];
    // The hand's own pair (arms cycle 12): the joint that keeps the palm's contact
    // patch flat while the elbow presses through it.
    private readonly HingeJoint2D[] wrist = new HingeJoint2D[2];
    private readonly Muscle[] wrFlex = new Muscle[2];
    private readonly Muscle[] wrExt = new Muscle[2];
    private readonly float[] wristInertia = new float[2];
    private readonly float[] wristHold = new float[2];
    private readonly Transform[] upper = new Transform[2];
    private readonly Transform[] hand = new Transform[2];
    private readonly Collider2D[] handCol = new Collider2D[2];
    private readonly float[] elbowInertia = new float[2];
    private readonly float[] shoulderInertia = new float[2];
    private readonly int[] phase = new int[2];
    private readonly float[] phaseAge = new float[2];
    private readonly float[] strokeStartElbow = new float[2];
    // Last shoulder target emitted while the body is down, and whether it has been
    // set yet: the aim slew limit needs the previous target to work from.
    private readonly float[] aimTargetPrev = new float[2];
    private readonly bool[] aimSlewSeen = new bool[2];
    private readonly float[] elbowSlewPrev = new float[2];
    private readonly float[] strokeStartShoulder = new float[2];
    private readonly float[] strokeStartPelvisY = new float[2];
    private readonly float[] strokeStartShoulderY = new float[2];
    private readonly float[] strokeShoulderLift = new float[2];
    private readonly float[] lostContactTime = new float[2];
    private readonly float[] strokeStall = new float[2];
    private readonly int[] aimSignVote = new int[2];
    // Elbow branch in use per side (0 = upper arm at beta+alpha, 1 = beta-alpha);
    // -1 before the first solve. Kept between steps so the branch cannot flap.
    private readonly int[] aimBranch = new int[2] { -1, -1 };
    private readonly float[] aimHavePrev = new float[2];
    private readonly float[] aimThPrev = new float[2];
    private readonly bool[] aimSignSeen = new bool[2];
    private readonly int[] strokeCount = new int[2];
    private readonly float[] strokeExt = new float[2];
    private readonly float[] strokeShoulderExt = new float[2];
    // Run-scoped copies of the three stroke maxima above (arms cycle 10). Never
    // reset, including by BeginStroke: this is what the published maxima read,
    // so a trial that ends mid-stroke still reports what the press achieved.
    private readonly float[] runStrokeExtMax = new float[2];
    private readonly float[] runShoulderExtMax = new float[2];
    private readonly float[] runShoulderLiftMax = new float[2];
    private readonly float[] contactSamples = new float[2];
    private readonly float[] contactHits = new float[2];
    // Stroke re-engages taken from the palm that is still holding the slab instead
    // of a fresh plant (arms cycle 14). Read back on the `-armDebug 1` line.
    private readonly int[] twoPalmReengages = new int[2];
    // Strike state (punch cycle p1): the stroke in force has its own clock
    // (< 0 = no stroke in flight) and the emitted target of the last step (the
    // rate cap works from it), plus the side the request resolved to at the fire.
    private MotionIntent motionIntent;
    private float punchClock = -1f;
    private float punchEmitShoulder;
    private float punchEmitElbow;
    private float punchEmitWrist;
    private int punchSideIndex = SideR;
    // Matched press window state: latched on the down edge, monotone maxima.
    private bool downPrev;
    private bool winStarted;
    private float winSeconds;
    private float winSamples;
    private readonly float[] winContactHits = new float[2];
    private readonly float[] winPivotY = new float[2];
    private readonly float[] winPivotLow = new float[2];
    private readonly float[] winRise = new float[2];
    private float winPelvisY;
    private float winPelvisX;
    private float winPelvisLow;
    private readonly float[] outShoulder = new float[2];
    private readonly float[] outElbow = new float[2];
    private readonly float[] outSlew = new float[2];

    // Walk swing conditioner state (ARMS cycle, walk cycle 8). One filter chain
    // per side; coefficients are rebuilt at every walk gate opening so a CLI
    // override is picked up without a rebuild. The emit arrays are the slew
    // states of the emitted targets - the values the muscles receive.
    private struct Biquad
    {
        public float b0, b1, b2, a1, a2;
        private float x1, x2, y1, y2;

        // RBJ low-pass, normalized by a0 (already folded into every term).
        public static Biquad LowPass(float fcHz, float q, float dt)
        {
            float w0 = 2f * Mathf.PI * Mathf.Max(0.05f, fcHz) * dt;
            float alpha = Mathf.Sin(w0) / (2f * Mathf.Max(0.1f, q));
            float cw = Mathf.Cos(w0);
            float inv = 1f / (1f + alpha);
            Biquad f;
            f.b0 = (1f - cw) * 0.5f * inv;
            f.b1 = (1f - cw) * inv;
            f.b2 = f.b0;
            f.a1 = -2f * cw * inv;
            f.a2 = (1f - alpha) * inv;
            f.x1 = 0f; f.x2 = 0f; f.y1 = 0f; f.y2 = 0f;
            return f;
        }

        // RBJ peaking EQ. Unit gain at DC, so a chain flushed with a constant
        // still passes that constant through unchanged.
        public static Biquad Peaking(float fcHz, float q, float gainDb, float dt)
        {
            float w0 = 2f * Mathf.PI * Mathf.Max(0.05f, fcHz) * dt;
            float alpha = Mathf.Sin(w0) / (2f * Mathf.Max(0.1f, q));
            float cw = Mathf.Cos(w0);
            float a = Mathf.Pow(10f, gainDb / 40f);
            float inv = 1f / (1f + alpha / a);
            Biquad f;
            f.b0 = (1f + alpha * a) * inv;
            f.b1 = -2f * cw * inv;
            f.b2 = (1f - alpha * a) * inv;
            f.a1 = -2f * cw * inv;
            f.a2 = (1f - alpha / a) * inv;
            f.x1 = 0f; f.x2 = 0f; f.y1 = 0f; f.y2 = 0f;
            return f;
        }

        // Identity section (mirror mode): fills a chain slot that carries no filtering.
        public static Biquad Pass()
        {
            Biquad f;
            f.b0 = 1f; f.b1 = 0f; f.b2 = 0f; f.a1 = 0f; f.a2 = 0f;
            f.x1 = 0f; f.x2 = 0f; f.y1 = 0f; f.y2 = 0f;
            return f;
        }

        public float Step(float x)
        {
            float y = b0 * x + b1 * x1 + b2 * x2 - a1 * y1 - a2 * y2;
            x2 = x1; x1 = x;
            y2 = y1; y1 = y;
            return y;
        }

        // History set to a constant input, i.e. the chain starts in the steady
        // state it would have reached: the walk onset gets no filter transient.
        public void Flush(float x)
        {
            x1 = x; x2 = x; y1 = x; y2 = x;
        }
    }

    private readonly Biquad[] swingChainL = new Biquad[1];
    private readonly Biquad[] swingChainR = new Biquad[3];
    private bool swingFilterReady;
    private readonly float[] walkEmitShoulder = new float[2];
    private readonly float[] walkEmitElbow = new float[2];
    // True once the emit states are tracking a live gate: the frame the gate
    // opens snaps them to the target (no start-up walk from 0), after that they
    // are rate-limited.
    private bool swingEmitTracking;
    // C57 — the elbow's own walk cycle: the previous frame's same-side swing,
    // per side, and whether the rate read is primed. Not primed on the first
    // frame of every gate opening: no rate is taken from the closed-gate zero
    // state, so the opening frame is bit-for-bit the pre-cycle expression.
    private readonly float[] walkPhasePrev = new float[2];
    private bool walkPhasePrimed;

    private float shTorqueBase = 40f, elTorqueBase = 30f;
    // The hand's own ceiling as the human build set it (Human.wristMuscleTorque,
    // 10 N.m). Kept separate for the same reason the other two are: the press
    // boost must be able to restore it, and the damper must be read from it.
    private float wrTorqueBase = 10f;
    private float hipSpeedHold;
    private float walkBlend;
    // Filled lazily from the human's own walk driver. The swing must follow the
    // walker's decision to walk, not the hip jitter the swing itself creates.
    private StepPhaseDriver stepDriver;
    private readonly float[] hipMean = new float[2];
    private readonly float[] hipDev = new float[2];
    private readonly float[] hipVar = new float[2];
    private readonly bool[] hipStatsReady = new bool[2];
    private float pressRequestTime = -100f;
    private float pelvisStartY;
    private bool down;
    private bool bound;
    private Transform pelvisBody;
    private float groundTopY = -2f;
    private readonly float[] linkL1 = new float[2];
    private readonly float[] linkL2 = new float[2];
    private readonly float[] plantX = new float[2];
    private readonly float[] plantY = new float[2];
    // Highest shoulder pivot this side has held with the palm on the slab during
    // the stroke in flight (arms cycle 13); seeded from the current pivot at every
    // latch, so it never carries a posture from an older stroke or from the fall.
    private readonly float[] plantPivotYBest = new float[2];

    private const string ArgShoulderSwing = "-armSwing";
    private const string ArgElbowAmp = "-armElbowSwing";
    private const string ArgElbowMid = "-armElbowMid";
    private const string ArgElbowLead = "-armElbowLead";
    private const string ArgElbowFloor = "-armElbowFloor";
    private const string ArgElbowCeil = "-armElbowCeil";
    private const string ArgElbowKnee = "-armElbowKnee";
    private const string ArgSign = "-armSign";
    private const string ArgIps = "-armIps";
    private const string ArgCross = "-armCross";
    private const string ArgCrossR = "-armCrossR";
    private const string ArgWalkGain = "-armWalkGain";
    private const string ArgPush = "-armPush";
    private const string ArgPushScale = "-armPushScale";
    private const string ArgAfterfallScale = "-armAfterfallScale";
    private const string ArgDamperBase = "-armDamperBase";
    private const string ArgWristHold = "-armWristHold";
    private const string ArgFallTilt = "-armFallTilt";
    private const string ArgPlantSide = "-armPlantSide";
    private const string ArgPlantShare = "-armPlantShare";
    private const string ArgPlantStop = "-armPlantStop";
    private const string ArgPlantReach = "-armPlantReach";
    private const string ArgHighLatch = "-armHighLatch";
    private const string ArgPressDepth = "-armPressDepth";
    private const string ArgSinkCap = "-armPressSinkCap";
    private const string ArgPressMode = "-armPressMode";
    private const string ArgDepthExtra = "-armDepthExtra";
    private const string ArgDepthRate = "-armDepthRate";
    private const string ArgPalmLive = "-armPalmLive";
    private const string ArgTwoPalm = "-armTwoPalm";
    private const string ArgHipTau = "-armHipTau";
    private const string ArgStrokeRate = "-armStrokeRate";
    private const string ArgStrokeMin = "-armStrokeMin";
    private const string ArgAimGain = "-armAimGain";
    private const string ArgPressShoulder = "-armPressShoulder";
    private const string ArgPressElbow = "-armPressElbow";
    private const string ArgLiftHold = "-armLiftHold";
    private const string ArgLiftClear = "-armLiftClear";
    private const string ArgElbowSlew = "-armElbowSlew";
    private const string ArgSwingLowPassL = "-armSwingLpL";
    private const string ArgSwingLowPassR = "-armSwingLpR";
    private const string ArgSwingPeakDb = "-armSwingPeakDb";
    private const string ArgSwingMirror = "-armSwingMirror";
    private const string ArgSwingSlew = "-armSwingSlew";
    private const string ArgSwingElbowSlew = "-armSwingElbowSlew";
    public float armDebug;
    private const string ArgDebug = "-armDebug";
    // The strike's own surface (punch cycle p1): -punch* never collides with the
    // walk's -arm* names, and the trial's own schedule flags (-punch, -punchTime,
    // -punchHold) live in HeadlessTrial; these tune the stroke itself.
    private const string ArgPunchWind = "-punchWind";
    private const string ArgPunchExtend = "-punchExtend";
    private const string ArgPunchReturn = "-punchReturn";
    private const string ArgPunchInterval = "-punchInterval";
    private const string ArgPunchShoulderBack = "-punchShoulderBack";
    private const string ArgPunchShoulderHit = "-punchShoulderHit";
    private const string ArgPunchElbowBack = "-punchElbowBack";
    private const string ArgPunchElbowHit = "-punchElbowHit";
    private const string ArgPunchWristBack = "-punchWristBack";
    private const string ArgPunchWristHit = "-punchWristHit";
    private const string ArgPunchSide = "-punchSide";
    private const string ArgPunchTorque = "-punchTorque";
    private const string ArgPunchGain = "-punchGain";
    private const string ArgPunchSlew = "-punchSlew";
    private const string ArgPunchDriveSlew = "-punchDriveSlew";
    private const string ArgPunchCounter = "-punchCounter";
    private const string ArgPunchCounterMax = "-punchCounterMax";
    private const string ArgGetUpSlap = "-getUpSlap";
    private const string ArgGetUpSlapLift = "-getUpSlapLift";
    private const string ArgGetUpSlapDrive = "-getUpSlapDrive";
    private const string ArgGetUpSlapSettle = "-getUpSlapSettle";
    private const string ArgGetUpSlapSlew = "-getUpSlapSlew";
    private const string ArgGetUpSlapSettleDeg = "-getUpSlapSettleDeg";
    private const string ArgGetUpSlapSettleMps = "-getUpSlapSettleMps";
    private const string ArgGetUpSlapQuiet = "-getUpSlapQuiet";

    void Awake()
    {
        ParseArgs();
        Bind();
    }

    // Idempotent: Human.BuildHuman calls it once the body exists.
    public void Bind()
    {
        if (bound) return;
        bound = true;

        balance = GetComponent<BalanceController>();
        vestibular = GetComponent<VestibularSystem>();

        hip[SideL] = JointOn("LeftLegThigh");
        hip[SideR] = JointOn("RightLegThigh");
        shoulder[SideL] = JointOn("LeftArmUpper");
        shoulder[SideR] = JointOn("RightArmUpper");
        elbow[SideL] = JointOn("LeftArmLower");
        elbow[SideR] = JointOn("RightArmLower");

        for (int i = 0; i < 2; i++)
        {
            string side = i == SideL ? "Left" : "Right";
            MuscleFlexorPair(side + "ArmUpper", out shFlex[i], out shExt[i]);
            MuscleFlexorPair(side + "ArmLower", out elFlex[i], out elExt[i]);
            MuscleFlexorPair(side + "ArmHand", out wrFlex[i], out wrExt[i]);
            wrist[i] = JointOn(side + "ArmHand");
            upper[i] = transform.Find(side + "ArmUpper");
            hand[i] = transform.Find(side + "ArmHand");
            handCol[i] = hand[i] != null ? hand[i].GetComponent<Collider2D>() : null;
            phase[i] = PhaseIdle;
            // Link lengths for the two-link solve. The joint anchors live in each
            // segment's own frame, so the pivots are exact, not centre-to-centre.
            if (shoulder[i] != null && shoulder[i].attachedRigidbody != null)
            {
                Transform armRig = shoulder[i].attachedRigidbody.transform;
                Vector2 shoulderPivot = (Vector2)armRig.position + (Vector2)(armRig.rotation * (Vector3)shoulder[i].anchor);
                Vector2 elbowPivot = shoulderPivot + Vector2.down * 0.33f;
                Transform foreRig = elbow[i] != null && elbow[i].attachedRigidbody != null ? elbow[i].attachedRigidbody.transform : null;
                if (foreRig != null)
                {
                    elbowPivot = (Vector2)foreRig.position + (Vector2)(foreRig.rotation * (Vector3)elbow[i].anchor);
                }
                linkL1[i] = Vector2.Distance(shoulderPivot, elbowPivot);
                if (hand[i] != null) linkL2[i] = Vector2.Distance(elbowPivot, (Vector2)hand[i].position);
            }
            if (linkL1[i] <= 0.01f) linkL1[i] = 0.33f;
            if (linkL2[i] <= 0.01f) linkL2[i] = 0.34f;
        }

        if (shFlex[SideL] != null) shTorqueBase = shFlex[SideL].maxTorque;
        if (elFlex[SideL] != null) elTorqueBase = elFlex[SideL].maxTorque;
        if (wrFlex[SideL] != null) wrTorqueBase = wrFlex[SideL].maxTorque;

        pelvisBody = transform.Find("Pelvis");
        if (pelvisBody != null) pelvisStartY = pelvisBody.position.y;
        GameObject ground = GameObject.Find("Ground");
        if (ground != null)
        {
            Collider2D groundCollider = ground.GetComponent<Collider2D>();
            if (groundCollider != null) groundTopY = groundCollider.bounds.max.y;
        }
    }

    private HingeJoint2D JointOn(string name)
    {
        Transform t = transform.Find(name);
        return t != null ? t.GetComponent<HingeJoint2D>() : null;
    }

    private void MuscleFlexorPair(string segmentName, out Muscle flexor, out Muscle extensor)
    {
        flexor = null;
        extensor = null;
        Transform t = transform.Find(segmentName);
        if (t == null) return;
        Muscle[] all = t.GetComponents<Muscle>();
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] == null) continue;
            if (all[i].direction > 0f) flexor = all[i];
            else extensor = all[i];
        }
    }

    // The get-up (BalanceController) calls this every step while it wants the
    // palm press. intensity 0..1. Returns true when the press is running with
    // both palms in real contact.
    public bool RequestArmPress(float intensity)
    {
        float level = Mathf.Clamp01(intensity);
        if (level >= 0.01f)
        {
            PressRequest = level;
            pressRequestTime = Time.fixedTime;
        }
        return HandContactL && HandContactR;
    }

    void FixedUpdate()
    {
        if (!bound) Bind();
        if (shoulder[SideL] == null || elbow[SideL] == null) return;

        float dt = Time.fixedDeltaTime;
        float tilt = vestibular != null ? vestibular.GetBodyTilt() : 0f;
        bool requestLive = PressRequest >= 0.01f
            && Time.fixedTime - pressRequestTime <= pressRequestHoldSeconds;
        down = Mathf.Abs(tilt) >= fallTiltDeg || requestLive;

        SampleHandContact();
        // The matched window is latched on the down edge and sampled while the
        // body is down - before the press runs, so the frame the body goes down
        // is in the window on both arms of the A/B.
        if (down && !downPrev) BeginPressWindow();
        downPrev = down;
        if (down) SamplePressWindow(dt);
        UpdateWalkSwing(dt);
        UpdatePress(dt);
        // THE STRIKE, last: it OVERRIDES the striking side's targets on the step
        // it is active (one writer per joint, R3) and writes nothing when off.
        UpdatePunch(dt);
        // THE GET-UP MAT SLAP (cycle 112), last: while its stroke is in flight
        // it overrides BOTH arms' targets (one writer per joint) and it writes
        // nothing when off.
        UpdateGetUpSlap(dt);
        WriteMuscles();
    }

    // ─── walk swing ───

    private void UpdateWalkSwing(float dt)
    {
        if (stepDriver == null) stepDriver = GetComponent<StepPhaseDriver>();
        bool walkActive = stepDriver != null && stepDriver.walkActive;

        float hipSpeed = 0f;
        for (int i = 0; i < 2; i++)
        {
            if (hip[i] != null)
                hipSpeed = Mathf.Max(hipSpeed, Mathf.Abs(hip[i].jointSpeed));
        }
        if (hipSpeed >= walkHipSpeedDegPerSec) hipSpeedHold = walkHoldSeconds;
        else hipSpeedHold = Mathf.Max(0f, hipSpeedHold - dt);

        // The gate needs a moving hip AND a walk the walker actually asked for.
        // The hip test alone is a trap: the swing shakes the torso, the torso
        // drives the hips, so the hip speed that switched the swing on is made by
        // the swing itself. In a quiet stand that loop latched the swing on for
        // 99.6% of the frames (18.09 21:15: shoulder RMS 181.7 deg/s, elbow 311.6,
        // torsoAngVel 63.0) while the same build with -armSwing 0 stands at
        // 7.4 / 21.5 / 3.9, i.e. exactly the pre-regression stand. The walker's
        // own walkActive reads 0 in all 8001 stand frames and 1 in 7000 of 8001
        // walk frames, so it separates the two states instead of inverting them.
        float target = hipSpeedHold > 0f && walkActive && !down ? 1f : 0f;
        walkBlend = Mathf.MoveTowards(walkBlend, target, dt / Mathf.Max(0.05f, walkRampSeconds));
        WalkSwingLevel = walkBlend;

        // The swing follows the hips' OSCILLATION, not their mean position, and
        // each side is standardised by its own running deviation: measured, the
        // left thigh swings 1.8x the right one, and with a shared scale the
        // left arm's two terms cancel out and the drive emits nothing (p_arms8:
        // left shoulder target +-2 deg against the right one's +-20).
        float alpha = 1f - Mathf.Exp(-dt / Mathf.Max(0.05f, hipStatsTau));
        for (int i = 0; i < 2; i++)
        {
            if (hip[i] == null)
            {
                hipDev[i] = 0f;
                continue;
            }
            float x = hip[i].jointAngle;
            if (!hipStatsReady[i])
            {
                hipMean[i] = x;
                hipVar[i] = 0f;
                hipStatsReady[i] = true;
            }
            float d = x - hipMean[i];
            hipMean[i] += alpha * d;
            hipVar[i] += alpha * (d * d - hipVar[i]);
            float sd = Mathf.Sqrt(Mathf.Max(hipVar[i], minHipVarianceDeg2));
            hipDev[i] = Mathf.Clamp((x - hipMean[i]) / sd, -2.5f, 2.5f);
        }

        float dir = swingSign >= 0f ? 1f : -1f;
        // Positive phase = that arm is BACK. The acceptance pairing: the left
        // shoulder is driven AGAINST the right thigh, so the opposite hip enters
        // with a negative coefficient. The same-side term carries the opposite
        // sign and keeps the two arms in counter phase with each other in a
        // window where the legs move together (corr(hipL,hipR) near +1 otherwise
        // forces both shoulders into one signal — the +0.99 of p_arms2). The two
        // ratios are per side (crossWeightR) because the two hips are not mirror
        // signals in this rig, and each side is standardised by its own deviation.
        // Mirror mode (req-200): the right mix takes the left cross weight (crossWeightR ignored),
        // so scaleR == scaleL and backR is backL with the sides swapped. Off = the per-side pair.
        float crossWeightRight = armSwingMirror > 0.5f ? crossWeight : crossWeightR;
        float scaleL = Mathf.Max(0.1f, ipsWeight + crossWeight);
        float scaleR = Mathf.Max(0.1f, ipsWeight + crossWeightRight);
        float backL = dir * (ipsWeight * hipDev[SideL] - crossWeight * hipDev[SideR]) / scaleL;
        float backR = dir * (ipsWeight * hipDev[SideR] - crossWeightRight * hipDev[SideL]) / scaleR;
        // ARMS cycle (walk cycle 8): the phase the muscles are asked to chase is
        // band-limited BEFORE the blend, so the walkBlend gate and the 0.3 s
        // ramp are untouched and the gated-out path is bit-for-bit the
        // pre-cycle expression (a stand keeps emitting shoulderBase exactly).
        // No state survives a gate close: the filter is rebuilt and flushed at
        // the next opening, which is also where a CLI override lands.
        float rawL = Mathf.Clamp(backL, -1f, 1f);
        float rawR = Mathf.Clamp(backR, -1f, 1f);
        float phaseL = rawL;
        float phaseR = rawR;
        if (walkBlend > 0.001f)
        {
            if (!swingFilterReady)
            {
                swingFilterReady = true;
                BuildWalkSwingFilters(dt);
                FlushChain(swingChainL, rawL);
                FlushChain(swingChainR, rawR);
                walkPhasePrimed = false;
            }
            phaseL = StepChain(swingChainL, rawL);
            phaseR = StepChain(swingChainR, rawR);
        }
        else
        {
            swingFilterReady = false;
            walkPhasePrimed = false;
        }
        ArmPhaseL = phaseL * walkBlend;
        ArmPhaseR = phaseR * walkBlend;

        float shoulderBase = balance != null ? balance.shoulderBaseAngle : 0f;
        float elbowBase = balance != null ? balance.elbowBaseAngle : -20f;
        // Elbow: flex on the forward swing, release on the back swing. Both
        // sides ride their own phase, so the two elbows counter each other.
        //
        // The sign, measured (19.09, arms cycle 7, `Tools/arm-elbow-shared-check.py`):
        // this line used to carry `-(mid + amp*phase)`, which flexes the elbow on
        // the BACK swing - the opposite of the sentence above and of this file's
        // own convention (negative = flexion = limb forward, phase +1 = that arm
        // is BACK). It reads off the records as a slope of -16.000 deg per unit
        // phase against the shoulder line's +34.000 (R2 1.000 on both, p_arms15),
        // and it made the elbow ride the thigh it is meant to oppose: measured
        // elbowL-hipR +0.702 / elbowR-hipL +0.840 on p_arms81 (walk 0.1-7.3 s)
        // where the shoulders of the same window read -0.553 / -0.745. The mean
        // is unchanged (elbowBase - walkElbowMid), only the swing's sign is.
        //
        // NOTE: the elbow PAIR's correlation is not set here and cannot be set by
        // a sign: both elbow targets are +-phaseNow, so corr(tgtElbL, tgtElbR)
        // equals corr(tgtShL, tgtShR) exactly (p_arms15: -0.843 / -0.843), and a
        // common sign flip on the two drives is invisible to it. What separates
        // the two elbows is corr(armPhaseL, armPhaseR) - the hips' own
        // correlation, which the walk worker's gait owns.
        for (int i = 0; i < 2; i++)
        {
            float phaseNow = i == SideL ? ArmPhaseL : ArmPhaseR;
            // C57: the elbow's own phase, from the same-side swing's own rate
            // (see walkElbowLeadSeconds). The first frame of a gate opening is
            // unprimed and takes no rate; every later frame reads the frame-to-
            // frame change of the swing above.
            float phaseRate = walkPhasePrimed ? (phaseNow - walkPhasePrev[i]) / dt : 0f;
            walkPhasePrev[i] = phaseNow;
            float shoulderTarget = (balance != null ? balance.ShoulderBaseFor(i) : shoulderBase)
                + walkShoulderSwing * phaseNow;
            float elbowTarget;
            if (walkElbowFloorDeg > 0f || walkElbowCeilDeg > 0f)
            {
                // C65: the band holds the ABSOLUTE flexion (deg from the straight arm)
                // inside [floor, ceil]. The OFF branch below is the pre-cycle expression
                // verbatim (bit for bit).
                float absFlex = -(elbowBase - walkElbowMid
                    + walkElbowAmp * (phaseNow + walkElbowLeadSeconds * phaseRate));
                absFlex = SoftBand(absFlex,
                    walkElbowFloorDeg > 0f ? walkElbowFloorDeg : -1e6f,
                    walkElbowCeilDeg > 0f ? walkElbowCeilDeg : 1e6f,
                    walkElbowBandKneeDeg);
                elbowTarget = -absFlex;
            }
            else
            {
                elbowTarget = elbowBase - walkElbowMid
                    + walkElbowAmp * (phaseNow + walkElbowLeadSeconds * phaseRate);
            }
            // The rate ceiling is the last line of defence: whatever the phase
            // does, no 5 ms frame asks a muscle for more than this step. The
            // frame the gate opens the state is snapped to the target (a slew
            // state left at 0 would drag each arm through a start-up ramp);
            // gated out, both values are assigned straight through, i.e.
            // exactly the pre-cycle expressions.
            if (walkBlend > 0.001f && swingEmitTracking)
            {
                walkEmitShoulder[i] = Mathf.MoveTowards(walkEmitShoulder[i], shoulderTarget,
                                                        walkShoulderSlewPerSecond * dt);
                walkEmitElbow[i] = Mathf.MoveTowards(walkEmitElbow[i], elbowTarget,
                                                     walkElbowSlewPerSecond * dt);
            }
            else
            {
                walkEmitShoulder[i] = shoulderTarget;
                walkEmitElbow[i] = elbowTarget;
            }
            outShoulder[i] = walkEmitShoulder[i];
            outElbow[i] = walkEmitElbow[i];
            outSlew[i] = walkDriveSlewPerSecond;
        }
        // Read one frame behind on purpose: the loop above sees the gate as the
        // previous frame left it, which is what makes the opening frame snap.
        swingEmitTracking = walkBlend > 0.001f;
        walkPhasePrimed = true;
    }

    // Builds the per-side swing conditioner at a walk gate opening. LEFT: one
    // 2nd-order Butterworth section at the left cutoff (pairing preservation,
    // see the field block). RIGHT: two sections (Butterworth 4th order, Q
    // 0.5412 / 1.3065) plus the peaking EQ that restores the swing the steep
    // filter costs. Both chains have unit gain at DC.
    private void BuildWalkSwingFilters(float dt)
    {
        if (armSwingMirror > 0.5f)
        {
            // Mirror mode (req-200): the right chain IS the left one; the spare slots stay in the
            // array as identity sections so the call sites do not move.
            swingChainL[0] = Biquad.LowPass(walkSwingLowPassLHz, 0.70710678f, dt);
            swingChainR[0] = Biquad.LowPass(walkSwingLowPassLHz, 0.70710678f, dt);
            swingChainR[1] = Biquad.Pass();
            swingChainR[2] = Biquad.Pass();
            return;
        }
        swingChainL[0] = Biquad.LowPass(walkSwingLowPassLHz, 0.70710678f, dt);
        swingChainR[0] = Biquad.LowPass(walkSwingLowPassRHz, 0.54119610f, dt);
        swingChainR[1] = Biquad.LowPass(walkSwingLowPassRHz, 1.30656296f, dt);
        swingChainR[2] = Biquad.Peaking(walkSwingPeakHz, walkSwingPeakQ, walkSwingPeakDb, dt);
    }

    private static float StepChain(Biquad[] chain, float x)
    {
        for (int s = 0; s < chain.Length; s++)
            x = chain[s].Step(x);
        return x;
    }

    private static void FlushChain(Biquad[] chain, float x)
    {
        for (int s = 0; s < chain.Length; s++)
            chain[s].Flush(x);
    }

    // C65 soft band: smooth saturation (softplus form) - the identity well inside the
    // band, asymptotic to lo/hi at the edges, C1 everywhere, so the drive's rate ceiling
    // sees no step (a hard clamp on this signal slams; see walk cycle 8's conditioner).
    private static float SoftBand(float x, float lo, float hi, float knee)
    {
        float k = Mathf.Max(0.0001f, knee);
        return lo + k * Softplus((x - lo) / k) - k * Softplus((x - hi) / k);
    }

    // Softplus with the linear guard past t = 20 (e^20 is 4.85e8; float-exact enough).
    private static float Softplus(float t)
    {
        return t > 20f ? t : Mathf.Log(1f + Mathf.Exp(t));
    }

    // ─── fall press ───

    // Two-link solve of the arm for a hand target: the shoulder and elbow joint
    // angles that put the palm on the target. The elbow hinge only flexes one
    // way, so the two mirror triangles about the target chord are not both
    // available -- and which one is available is NOT the same on the two sides
    // (measured p_arms34: the left arm's reachable world directions are -18..232
    // deg and the right's 110..360, so +alpha serves one side and -alpha the
    // other). Both branches give the SAME elbow angle, so the branch is chosen by
    // reachability of the shoulder angle -- no sign vote and no mirror
    // assumption, and a wrong branch is impossible.
    //
    // The chord is clamped to the SHORTEST chord the elbow's stop allows: a
    // target nearer than that is pressed INTO the slab (the solve keeps the
    // target's direction and pushes) instead of being folded onto a collided
    // stop, which is what left the former plant with an 18 deg interior angle
    // against a 30 deg stop and no push at all.
    private void SolveArm(int i, Vector2 target, out float shoulderTarget, out float elbowTarget)
    {
        shoulderTarget = shoulder[i] != null ? shoulder[i].jointAngle : 0f;
        elbowTarget = elbow[i] != null ? elbow[i].jointAngle : 0f;
        if (shoulder[i] == null || upper[i] == null) return;

        Vector2 shoulderPivot = ShoulderPivotWorld(i);
        Vector2 toTarget = target - shoulderPivot;
        if (toTarget.sqrMagnitude < 1e-8f) return;

        float minChord = MinChord(i);
        float maxChord = Mathf.Max(minChord + 0.01f, linkL1[i] + linkL2[i] - 0.01f);
        float dist = Mathf.Clamp(toTarget.magnitude, minChord, maxChord);

        float beta = Mathf.Atan2(toTarget.y, toTarget.x) * Mathf.Rad2Deg;
        float cosAlpha = (linkL1[i] * linkL1[i] + dist * dist - linkL2[i] * linkL2[i])
                         / (2f * linkL1[i] * dist);
        float cosElbow = (linkL1[i] * linkL1[i] + linkL2[i] * linkL2[i] - dist * dist)
                         / (2f * linkL1[i] * linkL2[i]);
        float alpha = Mathf.Acos(Mathf.Clamp(cosAlpha, -1f, 1f)) * Mathf.Rad2Deg;
        float interior = Mathf.Acos(Mathf.Clamp(cosElbow, -1f, 1f)) * Mathf.Rad2Deg;

        float candPlus, candMinus;
        bool okPlus = JointAngleForDirection(i, beta + alpha, out candPlus);
        bool okMinus = JointAngleForDirection(i, beta - alpha, out candMinus);
        // Keep the branch this arm is already holding while it stays legal (a
        // press posture is a posture, not a per-step decision: both branches can
        // be inside the shoulder limits at once, and a per-step choice flapped
        // between them -- measured p_arms35, armTargetShoulderL -150, +63, -159,
        // +57 -- so the PD chased a target 200 deg away on alternate steps and
        // the palm never landed).
        //
        // When a fresh choice is needed, take the branch that puts the ELBOW
        // HIGHER: that is the push-up posture, the arm bowing up over its own
        // palm, and it is the one whose extension can only drive the shoulder
        // away from the slab. Measured p_arms36: the "nearest angle" rule took
        // the other branch, whose elbow sat between the palm and the body, and
        // the first elbow extension threw the hand 0.9 m into the air instead of
        // loading it (contact had been 100 % the frame before).
        int pick;
        if (aimBranch[i] == 0 && okPlus) pick = 0;
        else if (aimBranch[i] == 1 && okMinus) pick = 1;
        else if (okPlus && okMinus)
            pick = ElbowHeight(i, beta + alpha) >= ElbowHeight(i, beta - alpha) ? 0 : 1;
        else if (okPlus) pick = 0;
        else if (okMinus) pick = 1;
        else pick = aimBranch[i] == 1 ? 1 : 0;
        aimBranch[i] = pick;
        shoulderTarget = pick == 0 ? candPlus : candMinus;
        elbowTarget = ClampElbow(i, interior - 180f);
    }

    // World height of the elbow for a given upper-arm direction: the elbow is L1
    // from the shoulder pivot along that direction.
    private float ElbowHeight(int i, float upperDirDeg)
    {
        Vector2 pivot = ShoulderPivotWorld(i);
        return pivot.y + linkL1[i] * Mathf.Sin(upperDirDeg * Mathf.Deg2Rad);
    }

    // Chord of the two-link arm at a given interior (elbow) angle, metres.
    private float ChordForInterior(int i, float interiorDeg)
    {
        float c = Mathf.Cos(Mathf.Clamp(interiorDeg, 5f, 175f) * Mathf.Deg2Rad);
        float sq = linkL1[i] * linkL1[i] + linkL2[i] * linkL2[i]
                   - 2f * linkL1[i] * linkL2[i] * c;
        float chord = Mathf.Sqrt(Mathf.Max(0.0001f, sq));
        return Mathf.Clamp(chord, 0.05f, linkL1[i] + linkL2[i] - 0.01f);
    }

    // Shortest chord this arm can make without sitting on the elbow's stop: a
    // chord exactly at the stop leaves the joint no torque to hold the palm down.
    private float MinChord(int i)
    {
        float foldMin = (elbow[i] != null && elbow[i].useLimits ? elbow[i].limits.min : -150f)
                        + plantFoldMarginDeg;
        return ChordForInterior(i, 180f + foldMin);
    }

    // World position of the shoulder hinge: the upper arm's pivot, not its centre.
    private Vector2 ShoulderPivotWorld(int i)
    {
        if (shoulder[i] == null || shoulder[i].attachedRigidbody == null)
            return upper[i] != null ? (Vector2)upper[i].position : Vector2.zero;
        Transform armRig = shoulder[i].attachedRigidbody.transform;
        return (Vector2)armRig.position + (Vector2)(armRig.rotation * (Vector3)shoulder[i].anchor);
    }

    private void UpdatePress(float dt)
    {
        float baseShoulder = balance != null ? balance.shoulderBaseAngle : 0f;
        float baseElbow = balance != null ? balance.elbowBaseAngle : -20f;

        for (int i = 0; i < 2; i++)
        {
            TrackAimSign(i);
            bool contact = i == SideL ? (HandContactL || HandNearGroundL)
                                      : (HandContactR || HandNearGroundR);
            bool realContact = i == SideL ? HandContactL : HandContactR;
            if (down)
            {
                contactSamples[i] += 1f;
                if (contact) contactHits[i] += 1f;
            }

            float shoulderTarget = walkBlend > 0.001f ? outShoulder[i]
                : (balance != null ? balance.ShoulderBaseFor(i) : baseShoulder);
            float elbowTarget = walkBlend > 0.001f ? outElbow[i] : baseElbow;
            float slew = walkBlend > 0.001f ? walkDriveSlewPerSecond : pressDriveSlewPerSecond;

            if (!orientTried)
            {
                orientTried = true;
                orient = GetComponent<PoseOrientation>();
            }
            bool orientRest = orient != null && orient.Valid && orient.LyingBack;
            if (pushEnabled > 0.5f && down && orientRest)
            {
                // ── THE SELF-ORIENTATION REST (round 17, addendum 8) ──
                // A back-lying body cannot press the slab the prone way: the plant solve
                // aims "forward" in the torso frame, which on a supine body points the
                // arm straight off the deck. Hold the arms along the body instead; the
                // press re-latches from a fresh point when the pose recovers.
                if (phase[i] != PhaseIdle)
                {
                    phase[i] = PhaseIdle;
                    aimBranch[i] = -1;
                    strokeStartElbow[i] = 0f;
                    lostContactTime[i] = 0f;
                }
                shoulderTarget = orient.sideShoulderDeg;
                elbowTarget = orient.sideElbowDeg;
            }
            else if (pushEnabled > 0.5f && down)
            {
                float elbowAngle = elbow[i] != null ? elbow[i].jointAngle : 0f;
                phaseAge[i] += dt;
                slew = pressDriveSlewPerSecond;
                // WALK CYCLE 45: the guard has 0.2-0.6 s to carry the palm from
                // the walk's own swing onto the slab ahead of the body - the
                // landed press slew is not that rate.
                if (fallGuardReach != 0f) slew = Mathf.Max(slew, FallGuardSlewDegPerSecond);
                if (realContact) lostContactTime[i] = 0f;
                else lostContactTime[i] += dt;

                // ARMS cycle 13: the highest shoulder pivot this side holds WITH the
                // palm on the slab is the posture the next latch is computed from.
                // Tracked only while the side is really loaded (Press/Hold AND a real
                // contact), so a pivot from the fall or from an unloaded lift can
                // never raise it.
                if (realContact && (phase[i] == PhasePress || phase[i] == PhaseHold))
                {
                    float pivotYNow = ShoulderPivotWorld(i).y;
                    if (pivotYNow > plantPivotYBest[i]) plantPivotYBest[i] = pivotYNow;
                }

                if (phase[i] == PhaseIdle)
                {
                    // A fall just latched: latch a plant point from the pose the
                    // body landed in, then reach for it. The latched point is a
                    // WORLD point and is not recomputed while the arm works on
                    // it: the former per-frame PalmCentreHeight update moved the
                    // target with the hand's own orientation, so the arm chased a
                    // point it kept moving.
                    ChoosePlantPoint(i);
                    phase[i] = PhasePlant;
                    phaseAge[i] = 0f;
                }

                // Keep the palm pressed: the target's Y follows the hand's CURRENT
                // thickness (see pressPalmLiveDepth). X is never touched, so this
                // cannot re-create the chasing target the plant latch exists to
                // prevent.
                if (pressPalmLiveDepth > 0f)
                {
                    plantY[i] = Mathf.Lerp(plantY[i], PalmCentreHeight(i),
                        Mathf.Clamp01(pressPalmLiveDepth));
                }

                if (phase[i] == PhaseLift)
                {
                    // Palm unloaded: the arm carries the hand CLEAR of the slab and
                    // translates it to the next plant point (liftClearanceMeters),
                    // then lowers it. Same solve as the plant, but no stroke may
                    // start here.
                    Vector2 liftTarget = PlantTarget(i);
                    liftTarget.y += Mathf.Max(0f, liftClearanceMeters);
                    SolveArm(i, liftTarget, out shoulderTarget, out elbowTarget);
                    if (phaseAge[i] >= liftHoldSeconds)
                    {
                        phase[i] = PhasePlant;
                        phaseAge[i] = 0f;
                    }
                }
                else if (phase[i] == PhasePlant)
                {
                    // Reach for the latched point until the palm is really on the
                    // slab. That contact is the only thing that starts a stroke.
                    SolveArm(i, PlantTarget(i), out shoulderTarget, out elbowTarget);
                    // WALK CYCLE 45: the guard's palm HOLDS its slab point. The
                    // stroke - which drives the point deeper and presses the body
                    // back up - belongs to the get-up, not to the catch: while the
                    // guard is live the arm GIVES as the shoulder comes down onto
                    // it. The guard ends (fallGuardReach back to 0) when the body is
                    // down, and the landed rule then starts the first stroke on the
                    // first contact exactly as before.
                    if (realContact && fallGuardReach == 0f)
                    {
                        BeginStroke(i, elbowAngle);
                    }
                    else if (phaseAge[i] >= plantReachSeconds)
                    {
                        // The body rolled and the latched point is out of reach:
                        // take a fresh one from where the shoulder is now.
                        ChoosePlantPoint(i);
                        phaseAge[i] = 0f;
                    }
                }
                else
                {
                    // Press/Hold: the palm sits on the slab at a latched world
                    // point and does not move. Press drives that point pressDepth
                    // INTO the slab, so the arm is always asking for more
                    // extension than the ground allows: the joints load against
                    // the block, and the only thing that can give is the shoulder
                    // -- the body rising. Hold keeps the same point unloaded. The
                    // solve is re-run every step (not frozen) so the arm keeps
                    // pointing at its own palm as the shoulder climbs.
                    Vector2 strokeTarget = PlantTarget(i);
                    bool depthRamp = pressStrokeMode >= 0.5f;
                    if (phase[i] == PhasePress)
                    {
                        strokeTarget.y -= pressDepth;
                        if (depthRamp)
                        {
                            // The latched point is driven deeper into the slab and
                            // BOTH joints keep solving onto it, so the hand stays on
                            // its own palm while the arm is forced to give. This is
                            // the difference from the elbow ramp below: measured
                            // p_arms40 with the ramp, the elbow opened -141 -> -18
                            // while the shoulder aimed at the palm, and the hand
                            // ended 0.39 m PAST the palm it was supposed to press on
                            // (arm lying flat on the ground at x 1.86 against the
                            // palm's 2.25) -- the arm walked its hand instead of
                            // loading it, which is the p_arms20 churn again.
                            strokeTarget.y -= Mathf.Min(pressDepthExtra,
                                pressDepthRate * phaseAge[i]);
                        }
                    }
                    // ROUND 15: the sink cap (pressSinkCap). The palm may be asked to
                    // press into the slab, but never more than the cap below its own
                    // bottom face - the defence against a thin deck plate (a 0.16 m
                    // hand box against a 0.06 m plate; sitB's ejection).
                    if (pressSinkCap > 0.001f && realContact)
                    {
                        float palmBottom = handCol[i] != null
                            ? handCol[i].bounds.min.y
                            : (hand[i] != null ? hand[i].position.y : strokeTarget.y);
                        strokeTarget.y = Mathf.Max(strokeTarget.y, palmBottom - pressSinkCap);
                    }
                    SolveArm(i, strokeTarget, out shoulderTarget, out elbowTarget);
                    shoulderTarget += pressShoulderBias;
                    if (!depthRamp)
                    {
                        // Former stroke: the elbow alone is commanded toward full
                        // extension while the palm is blocked by the slab. The ramp
                        // is direction agnostic on purpose: a folded elbow is
                        // NEGATIVE here (-116 deg at the plant, -4 deg straight), so
                        // the earlier Mathf.Max(pressElbowTarget, start + rate*age)
                        // picked the fully extended target from the very first frame
                        // and the limiter never engaged -- measured p_arms37, the
                        // elbow threw 107 deg in 0.09 s and the hand ended 1.24 m off
                        // its own palm.
                        float strokeGoal = pressElbowTarget;
                        if (pressStrokeDegPerSecond > 0f)
                        {
                            float room = pressElbowTarget - strokeStartElbow[i];
                            float ramp = pressStrokeDegPerSecond * phaseAge[i];
                            strokeGoal = strokeStartElbow[i] + Mathf.Clamp(room, -ramp, ramp);
                        }
                        elbowTarget = ClampElbow(i, strokeGoal);
                    }
                    else
                    {
                        // The depth ramp has given all it has when the target stops
                        // getting deeper; the pose is then held by the solve.
                        elbowTarget = ClampElbow(i, elbowTarget);
                    }

                    if (phase[i] == PhasePress)
                    {
                        TrackStroke(i, elbowAngle, dt);
                        bool strokeDone = phaseAge[i] >= pressMaxSeconds
                                          || (phaseAge[i] >= pressMinStrokeSeconds
                                              && strokeStall[i] >= pressStallSeconds);
                        bool slabLost = lostContactTime[i] >= pressLostContactSeconds
                                        && phaseAge[i] >= pressMinStrokeSeconds;
                        if (strokeDone || slabLost)
                        {
                            phase[i] = PhaseHold;
                            phaseAge[i] = 0f;
                            // The stall that ENDED the stroke must not carry into
                            // the hold, or the hold's own stall test would re-engage
                            // on the first frame after the cooldown and the press
                            // would churn (measured p_arms52 21.5-27 s: 0.5 s strokes
                            // delivering 0-7 deg). The hold measures its own stall.
                            strokeStall[i] = 0f;
                        }
                    }
                    else
                    {
                        // Hold: keep the loaded pose. Another stroke only after a
                        // dwell and only for a reason — the slab is gone, the body
                        // sank and refolded the arm, the elbow ran out of room
                        // (straight = the press has given all it has), or the elbow
                        // has STOPPED extending while the body is still down. That
                        // last one is the climbing loop, and it is measured: p_arms50
                        // with the three former reasons only, the elbow stopped at
                        // -73 deg, none of them ever fired, and the arm sat in Hold
                        // for 5775 of the 6447 fallen frames (2 strokes in 32 s,
                        // 0.03 m of shoulder rise). Measured p_arms20 the opposite
                        // mistake: re-engaging on the elbow angle alone ran 191
                        // strokes of 0.18 s and 2.7 deg each.
                        TrackStroke(i, elbowAngle, dt);
                        bool sank = elbowAngle < strokeStartElbow[i] + pressReengageDeg;
                        bool outOfRoom = elbowAngle >= pressElbowTarget - pressDoneMarginDeg;
                        bool stalled = strokeStall[i] >= pressStallSeconds;
                        // ARMS cycle 14: the two-palm hold. An arm still on the slab is
                        // a support, and one palm is not enough to lift a torso:
                        // measured p_arms81 (the newest arms build), 16 of the 18
                        // fall-backs of the pelvis >= 0.05 m hold BOTH palms for
                        // 0.0-17.1 % of the drop (13 exactly 0.0 %), the drop is carried
                        // by one palm in 11 of them, and one side sits in Plant/Lift at
                        // the top or the bottom of 17 of the 18 -- while the rises (the
                        // 29.5-30.4 s climb, pivot -1.789 -> -1.600 m) run with BOTH
                        // sides in Press/Hold. So while this side's palm is STILL on the
                        // slab the point is KEPT (no ChoosePlantPoint, no lift) and the
                        // stroke is re-engaged from it -- the 0.4 s dwell of Hold is
                        // kept, the load is not. A palm that has really left the slab
                        // (lostContactTime >= pressLostContactSeconds) re-plants exactly
                        // as before, which is what moves a useless point.
                        // On the same record the gate has 577 (L) / 791 (R) frames of
                        // Hold with the side's own palm on the slab to act on.
                        bool ownPalmHeld = lostContactTime[i] < pressLostContactSeconds;
                        if (phaseAge[i] >= pressReengageCooldownSeconds
                            && (lostContactTime[i] >= pressLostContactSeconds || sank
                                || outOfRoom || stalled))
                        {
                            if (pressTwoPalm > 0.5f && ownPalmHeld)
                            {
                                twoPalmReengages[i]++;
                                BeginStroke(i, elbowAngle);
                            }
                            else
                            {
                                ChoosePlantPoint(i);
                                phase[i] = PhaseLift;
                                phaseAge[i] = 0f;
                            }
                        }
                    }
                }
            }
            else if (!down && phase[i] != PhaseIdle)
            {
                // Standing again: hand the arms back to the balance controller.
                phase[i] = PhaseIdle;
                aimBranch[i] = -1;
                strokeStartElbow[i] = 0f;
                lostContactTime[i] = 0f;
            }

            if (pushEnabled > 0.5f && down)
            {
                // The aim itself is rate limited, and this is what keeps the joint
                // calm. The solve is an incremental aim: it reads the arm's own
                // measured world direction and asks the shoulder to move by the
                // error. While the body is still tumbling that reading swings with
                // the arm, the aim chases it and the PD chases the aim -- measured
                // p_arms38, the shoulder target moved 16 deg in one 5 ms step
                // (3200 deg/s) and the arm whirled at 610 deg/s, ten times the
                // calm walking value the brief measured.
                if (!aimSlewSeen[i])
                {
                    aimSlewSeen[i] = true;
                    aimTargetPrev[i] = shoulder[i] != null ? shoulder[i].jointAngle : shoulderTarget;
                    elbowSlewPrev[i] = elbow[i] != null ? elbow[i].jointAngle : elbowTarget;
                }
                if (pressDriveSlewPerSecond > 0f)
                {
                    float maxStep = pressDriveSlewPerSecond * dt;
                    shoulderTarget = aimTargetPrev[i]
                        + Mathf.Clamp(shoulderTarget - aimTargetPrev[i], -maxStep, maxStep);
                }
                aimTargetPrev[i] = shoulderTarget;
                // The elbow gets the same treatment it never had: the stroke is
                // still free to open 90 deg/s, but the drive can no longer ask
                // for a 450 deg/s step that the joint then chases.
                if (pressElbowSlewPerSecond > 0f)
                {
                    float maxElbowStep = pressElbowSlewPerSecond * dt;
                    elbowTarget = elbowSlewPrev[i]
                        + Mathf.Clamp(elbowTarget - elbowSlewPrev[i], -maxElbowStep, maxElbowStep);
                }
                elbowSlewPrev[i] = elbowTarget;
            }
            else
            {
                aimSlewSeen[i] = false;
            }

            bool orientActive = orientRest && pushEnabled > 0.5f && down;
            if (orientActive && !orientRestOn[i])
            {
                OrientRestEntries++;
                if (orientLog)
                    Debug.Log("ARMPRESS orient-rest t=" + Time.time.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)
                        + " side=" + (i == SideL ? "L" : "R")
                        + " pose=" + orient.StateName
                        + " torso=" + orient.TorsoDeg.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture));
            }
            orientRestOn[i] = orientActive;
            outShoulder[i] = shoulderTarget;
            outElbow[i] = elbowTarget;
            outSlew[i] = slew;
            if (i == SideL)
            {
                ArmTargetShoulderL = shoulderTarget;
                ArmTargetElbowL = elbowTarget;
                PressPhaseL = phase[i];
            }
            else
            {
                ArmTargetShoulderR = shoulderTarget;
                ArmTargetElbowR = elbowTarget;
                PressPhaseR = phase[i];
            }
        }

        if (contactSamples[SideL] > 0f)
            PressHandContactFractionL = contactHits[SideL] / contactSamples[SideL];
        if (contactSamples[SideR] > 0f)
            PressHandContactFractionR = contactHits[SideR] / contactSamples[SideR];
        PressStrokesL = strokeCount[SideL];
        PressStrokesR = strokeCount[SideR];
        // Run-scoped, read from the never-reset copies: a stroke in flight must
        // not be able to make the published maximum fall (arms cycle 10).
        PressStrokeExtensionL = runStrokeExtMax[SideL];
        PressStrokeExtensionR = runStrokeExtMax[SideR];
        PressStrokeShoulderExtL = runShoulderExtMax[SideL];
        PressStrokeShoulderExtR = runShoulderExtMax[SideR];
        PressShoulderLiftL = runShoulderLiftMax[SideL];
        PressShoulderLiftR = runShoulderLiftMax[SideR];
        PressLastStrokeExtensionL = strokeExt[SideL];
        PressLastStrokeExtensionR = strokeExt[SideR];
        PressLastStrokeShoulderExtL = strokeShoulderExt[SideL];
        PressLastStrokeShoulderExtR = strokeShoulderExt[SideR];
        PressLastStrokeShoulderLiftL = strokeShoulderLift[SideL];
        PressLastStrokeShoulderLiftR = strokeShoulderLift[SideR];

        if (armDebug > 0f) LogArmState();
    }

    // Number dump for the press, off unless -armDebug 1. One line per side every
    // 0.25 s while the arms are working: the geometry a later change has to be
    // argued from (pivot, latched palm, the fold the solve asks for, the joint
    // range it has to live in) next to where the palm actually is.
    private void LogArmState()
    {
        if (Time.frameCount % 50 != 0) return;
        for (int i = 0; i < 2; i++)
        {
            Vector2 pivot = ShoulderPivotWorld(i);
            Vector2 palm = PlantTarget(i);
            float sh = shoulder[i] != null ? shoulder[i].jointAngle : 0f;
            float el = elbow[i] != null ? elbow[i].jointAngle : 0f;
            float shLo = shoulder[i] != null && shoulder[i].useLimits ? shoulder[i].limits.min : 0f;
            float shHi = shoulder[i] != null && shoulder[i].useLimits ? shoulder[i].limits.max : 0f;
            float elLo = elbow[i] != null && elbow[i].useLimits ? elbow[i].limits.min : 0f;
            Vector2 handPos = hand[i] != null ? (Vector2)hand[i].position : Vector2.zero;
            Debug.Log(string.Format(
                "[ARM] t={0:F2} {1} ph={2} age={3:F2} touch={4} lost={5:F2} " +
                "pivot=({6:F2},{7:F2}) palm=({8:F2},{9:F2}) hand=({10:F2},{11:F2}) " +
                "sh={12:F1}[{13:F0},{14:F0}] el={15:F1}[{16:F0}] plantFold={17:F1} " +
                "tgt=({18:F1},{19:F1}) wr={20:F1}->{21:F1} act={22:F2} drop={23:F3} tilt={24:F1} two={25}",
                Time.fixedTime, i == SideL ? "L" : "R",
                phase[i], phaseAge[i], (i == SideL ? HandContactL : HandContactR) ? 1 : 0,
                lostContactTime[i], pivot.x, pivot.y, palm.x, palm.y, handPos.x, handPos.y,
                sh, shLo, shHi, el, elLo,
                i == SideL ? PressPlantFoldL : PressPlantFoldR, outShoulder[i], outElbow[i],
                i == SideL ? PressWristAngleL : PressWristAngleR,
                i == SideL ? PressWristHoldL : PressWristHoldR,
                i == SideL ? PressWristActL : PressWristActR,
                i == SideL ? PressPlantDropL : PressPlantDropR,
                i == SideL ? PressPlantTiltL : PressPlantTiltR,
                twoPalmReengages[i]));
        }
    }

    private void BeginStroke(int i, float elbowAngle)
    {
        phase[i] = PhasePress;
        phaseAge[i] = 0f;
        strokeStartElbow[i] = elbowAngle;
        strokeStartShoulder[i] = shoulder[i] != null ? shoulder[i].jointAngle : 0f;
        strokeStartPelvisY[i] = pelvisBody != null ? pelvisBody.position.y : 0f;
        strokeStartShoulderY[i] = ShoulderPivotWorld(i).y;
        strokeExt[i] = 0f;
        strokeShoulderExt[i] = 0f;
        strokeShoulderLift[i] = 0f;
        strokeStall[i] = 0f;
        // The stroke is NOT steered by a frozen aim any more: the shoulder keeps
        // solving onto the latched palm (Press/Hold in UpdatePress) and the elbow
        // is commanded straight. Measured p_arms25/34 with a frozen aim: the arm
        // was free to rotate about its own elbow, so "extending" it flew the HAND
        // 0.66 m off the slab instead of loading the palm, and the shoulder --
        // the thing a press is supposed to move -- rose 1.6 mm.
        strokeCount[i]++;
    }

    private void TrackStroke(int i, float elbowAngle, float dt)
    {
        // Extension is positive: the elbow angle grows toward 0.
        float ext = elbowAngle - strokeStartElbow[i];
        // One step of expected progress, not a flat 0.5 deg: with the stroke rate
        // limiter on (60 deg/s at 200 Hz = 0.3 deg/step) a fixed threshold trips on
        // every single step, which ended every stroke in p_arms37 at exactly the
        // minimum age and never let the load build.
        float stepDone = pressStrokeDegPerSecond > 0f
            ? pressStrokeDegPerSecond * dt * 0.5f
            : 0.5f;
        if (ext > strokeExt[i] + stepDone)
        {
            strokeExt[i] = ext;
            strokeStall[i] = 0f;
        }
        else
        {
            strokeStall[i] += dt;
        }
        // The run-scoped copy of every maximum is raised here and NEVER reset
        // (BeginStroke does not touch it), so the published maximum is the run's,
        // not the last stroke's (arms cycle 10).
        if (strokeExt[i] > runStrokeExtMax[i]) runStrokeExtMax[i] = strokeExt[i];
        float shoulderAngle = shoulder[i] != null ? shoulder[i].jointAngle : 0f;
        float shoulderExt = shoulderAngle - strokeStartShoulder[i];
        if (shoulderExt > strokeShoulderExt[i]) strokeShoulderExt[i] = shoulderExt;
        if (strokeShoulderExt[i] > runShoulderExtMax[i]) runShoulderExtMax[i] = strokeShoulderExt[i];
        float pelvisY = pelvisBody != null ? pelvisBody.position.y : 0f;
        float lift = pelvisY - strokeStartPelvisY[i];
        if (lift > PressLiftMax) PressLiftMax = lift;
        // The palm is latched on the slab through the stroke, so the shoulder
        // pivot rising is the push itself, independent of what the legs do.
        float shoulderLift = ShoulderPivotWorld(i).y - strokeStartShoulderY[i];
        if (shoulderLift > strokeShoulderLift[i]) strokeShoulderLift[i] = shoulderLift;
        if (strokeShoulderLift[i] > runShoulderLiftMax[i])
            runShoulderLiftMax[i] = strokeShoulderLift[i];
    }

    // ─── plant geometry ───

    private Vector2 PlantTarget(int i)
    {
        return new Vector2(plantX[i], plantY[i]);
    }

    // Where the palm's centre has to sit to press the slab: the hand's own live
    // half-height above the ground top, sunk handGroundSink below it, so the
    // collider is squeezed onto the ground instead of hovering over it.
    // Deliberately NOT a fixed half-height: measured p_arms24 the left hand
    // landed flat (an AABB half-height of 0.03, not the 0.08 the target assumed),
    // stopped 25 mm over the slab and tripped the estimator's slop band with no
    // real contact at all — while the right hand, one centimetre lower and
    // standing on its end, gripped.
    private float PalmCentreHeight(int i)
    {
        float halfY = 0.06f;
        if (hand[i] != null && handCol[i] != null)
            halfY = hand[i].position.y - handCol[i].bounds.min.y;
        return groundTopY - handGroundSink + Mathf.Max(0.01f, halfY);
    }

    // Latch the palm's world point for the coming stroke. The palm goes as close
    // to straight under the shoulder as the elbow's stop allows: the lift a
    // stroke delivers is (chord growth) * (how vertical the chord is), so a palm
    // planted sideways spends the extension on a horizontal shove. The lateral
    // offset is therefore only the length the arm CANNOT fold away -- the chord
    // at the stop plus plantStopMarginDeg of margin, and beyond that straight
    // down. Measured p_arms38: forcing the elbow to start 65 deg from straight
    // planted the palm 0.35 m out, pinned the chord 70 deg off vertical and
    // delivered 0.10 m of shoulder rise, with the arm lying flat on the slab.
    // The earlier rule ("the closest point the folded arm can reach") planted at
    // the stop itself and left the stroke nothing to press with: p_arms34 offset
    // 0.00, plantFold -142, 1.6 mm of rise.
    private void ChoosePlantPoint(int i)
    {
        // WALK CYCLE 45: the fall guard owns the point while it is live. 0 is the
        // landed rule, and every pre-existing command line.
        if (fallGuardReach != 0f)
        {
            GuardPlantPoint(i, fallGuardReach);
            return;
        }
        // CYCLE 100: the get-up's own reach - the same slab geometry (reach ahead
        // of the shoulder along the facing), stroke allowed. 0 = the landed rule.
        if (getUpPlantReach != 0f)
        {
            GuardPlantPoint(i, getUpPlantReach);
            return;
        }
        Vector2 pivot = ShoulderPivotWorld(i);
        aimBranch[i] = -1;   // a new latched point is a fresh posture
        float palmY = PalmCentreHeight(i);
        // ARMS cycle 13: the drop the chord rule below is computed from is the
        // posture this side has PROVEN it can hold with the palm on the slab, not
        // the sagged frame the latch lands on (a latch is taken when a stroke ended
        // and the palm was unloaded). Everything downstream -- the offset, the
        // plant fold, the push line -- follows from this one number.
        float latchPivotY = pivot.y;
        if (plantHighLatch > 0.5f && plantPivotYBest[i] > latchPivotY)
            latchPivotY = plantPivotYBest[i];
        float drop = Mathf.Max(0.02f, latchPivotY - palmY);
        float foldMin = (elbow[i] != null && elbow[i].useLimits ? elbow[i].limits.min : -150f);
        float interiorMin = Mathf.Clamp(180f + foldMin + plantStopMarginDeg, 10f, 170f);
        float chordStart = ChordForInterior(i, interiorMin);
        // The latched point goes UNDER the shoulder (plantLateralShare 0) and, when
        // the shoulder is too low over the slab for that to be reachable, at the
        // shortest chord the arm can still hold torque at. A palm that sits BESIDE
        // the shoulder for its own sake spends the elbow extension on a horizontal
        // shove instead of a lift: measured p_arms39 with the lateral-fill rule, a
        // shoulder 0.11 m over the slab latched a 0.29 m side point, the arm lay
        // flat (sh +45, el -103), and the whole body was shoved 2.0 m along the
        // ground (pivot x 0.65 -> 2.67) with the pelvis never leaving -1.86.
        float reach = MinChord(i) + plantReachMargin;
        float offset;
        if (plantLateralShare > 0.5f)
        {
            // Former fill rule, kept for an A/B on one build.
            float lateralFill = drop < chordStart
                ? Mathf.Sqrt(Mathf.Max(0f, chordStart * chordStart - drop * drop))
                : 0f;
            offset = Mathf.Clamp(lateralFill, 0f, plantSideMax);
        }
        else
        {
            // sqrt(reach^2 - drop^2): 0 when the shoulder is high enough to take
            // the point straight underneath (the press posture), and exactly the
            // arm's minimum chord when it is not -- the nearest point on the slab
            // the arm can push from at all.
            float needed = drop < reach
                ? Mathf.Sqrt(Mathf.Max(0f, reach * reach - drop * drop))
                : 0f;
            offset = Mathf.Clamp(needed, 0f, plantSideMax);
        }

        // The fold the latched point asks for: the stroke starts here, so this is
        // the room the press has. Logged beside the delivered elbow angle, which
        // is how a jammed joint is told from a loaded one. The chord is clamped
        // the way the solve clamps it, so the logged fold is the fold the SOLVE
        // asks for and not the fold of an unreachable point below the slab.
        float chord = Mathf.Clamp(Mathf.Sqrt(offset * offset + drop * drop),
                                  chordStart, linkL1[i] + linkL2[i] - 0.01f);
        float cosInterior = (linkL1[i] * linkL1[i] + linkL2[i] * linkL2[i] - chord * chord)
                            / (2f * linkL1[i] * linkL2[i]);
        float plantFold = Mathf.Acos(Mathf.Clamp(cosInterior, -1f, 1f)) * Mathf.Rad2Deg - 180f;

        if (i == SideL)
        {
            PressPlantOffsetL = offset;
            PressPlantFoldL = plantFold;
        }
        else
        {
            PressPlantOffsetR = offset;
            PressPlantFoldR = plantFold;
        }

        // Which side of the shoulder the palm lands on: the side the folded arm
        // already lies. The rig's arms are not mirror-reachable (measured p_arms34:
        // the left arm's world window is -18..232 deg, the right's 110..360), so a
        // hardcoded side plants half the falls across the body. The fallback is the
        // old fixed side when the hand is level with its own pivot.
        float side = i == SideL ? -1f : 1f;
        if (hand[i] != null && Mathf.Abs(hand[i].position.x - pivot.x) > 0.05f)
            side = hand[i].position.x >= pivot.x ? 1f : -1f;
        plantX[i] = pivot.x + side * offset;
        plantY[i] = palmY;
        // The push line this latch was computed from, read-only (arms cycle 13).
        // 0 deg = the palm is straight under the shoulder; the palm-ground friction
        // cone is atan(sqrt(handFriction x groundFriction)) = 36.9 deg on the
        // recorded 1.4 / 0.4, so a tilt beyond it slides instead of pressing.
        float tiltDeg = Mathf.Atan2(Mathf.Abs(offset), Mathf.Max(0.001f, drop)) * Mathf.Rad2Deg;
        if (i == SideL)
        {
            PressPlantDropL = drop;
            PressPlantTiltL = tiltDeg;
        }
        else
        {
            PressPlantDropR = drop;
            PressPlantTiltR = tiltDeg;
        }
        // The next latch measures its own stroke: the running best starts again
        // from the pivot of the frame the point was taken on.
        plantPivotYBest[i] = pivot.y;
        // The hand's own orientation is part of the plant (arms cycle 12). The
        // palm is a flat contact patch and the wrist is the joint that keeps it
        // flat; the angle the instant the point is taken is the angle that had the
        // palm on the slab, so it is latched with the point and not recomputed --
        // the same reason the point itself is latched.
        wristHold[i] = wrist[i] != null ? wrist[i].jointAngle : 0f;
    }

    // WALK CYCLE 45: the fall guard's own plant point. The landed latch takes the
    // palm's CURRENT height - a sagged posture - and lets the stroke ramp dig the
    // point 0.18 m deeper, which is why the recorded fall plants its palms under
    // the chest at the moment the body is already down. The guard's point is the
    // SLAB itself, fallGuardReach ahead of the shoulder: the arm extends toward
    // the ground line from the first frame the state arms the guard, the chord
    // runs out as the body descends, and the palm lands as early as the rig's own
    // 0.67 m chord allows - in FRONT of the body, not under it. Like the landed
    // point it is a WORLD point and is not recomputed while the arm works on it,
    // so the body's own forward travel folds the elbow over the planted palm:
    // that fold is the absorb (the stroke, which would press the body back up, is
    // suppressed while the guard is live - see the PhasePlant branch).
    // CYCLE 100: the reach parameter was split out of the walk cycle 45 guard so
    // the GET-UP can borrow the same slab geometry for its own plant reach
    // (getUpPlantReach) while keeping its stroke - the guard keeps its no-stroke
    // absorb (the fallGuardReach != 0 check in the PhasePlant branch).
    private void GuardPlantPoint(int i, float reach)
    {
        Vector2 pivot = ShoulderPivotWorld(i);
        aimBranch[i] = -1;
        float half = 0.04f;
        if (handCol[i] != null) half = Mathf.Max(0.01f, handCol[i].bounds.extents.y);
        plantX[i] = pivot.x + reach;
        plantY[i] = groundTopY + half;
        float drop = Mathf.Max(0.02f, pivot.y - plantY[i]);
        float chord = Mathf.Clamp(Mathf.Sqrt(reach * reach + drop * drop),
                                  MinChord(i), linkL1[i] + linkL2[i] - 0.01f);
        float cosInterior = (linkL1[i] * linkL1[i] + linkL2[i] * linkL2[i] - chord * chord)
                            / (2f * linkL1[i] * linkL2[i]);
        float plantFold = Mathf.Acos(Mathf.Clamp(cosInterior, -1f, 1f)) * Mathf.Rad2Deg - 180f;
        float tiltDeg = Mathf.Atan2(Mathf.Abs(reach), Mathf.Max(0.001f, drop))
                        * Mathf.Rad2Deg;
        if (i == SideL)
        {
            PressPlantOffsetL = reach;
            PressPlantFoldL = plantFold;
            PressPlantDropL = drop;
            PressPlantTiltL = tiltDeg;
        }
        else
        {
            PressPlantOffsetR = reach;
            PressPlantFoldR = plantFold;
            PressPlantDropR = drop;
            PressPlantTiltR = tiltDeg;
        }
        plantPivotYBest[i] = pivot.y;
        wristHold[i] = wrist[i] != null ? wrist[i].jointAngle : 0f;
    }

    // Running vote of how this side's world direction moves with its joint angle.
    // It is a DIAGNOSTIC ONLY and no longer drives the aim. Measured p_arms34:
    // the vote came out -1 on BOTH sides while the recorded columns show the true
    // relation dir = rest + theta on both (left rest 142.4, theta -71.2, have
    // 71.1; right rest 270.8, theta -135.5, have 135.4). The drive trusted the
    // vote, so every aim was inverted: the left palm could not reach its own
    // latched point (0.27 m short, phase "plant" for 33 s) and the right arm only
    // reached the slab by the accident of its hinge range. The vote is corrupted
    // by the torso's own rotation, which changes `have` without moving `theta`;
    // the model below has no vote in it at all.
    private void TrackAimSign(int i)
    {
        if (shoulder[i] == null || shoulder[i].attachedRigidbody == null) return;
        float have = UpperArmWorldDeg(i);
        float th = shoulder[i].jointAngle;
        if (aimSignSeen[i])
        {
            float dHave = Mathf.DeltaAngle(aimHavePrev[i], have);
            float dTh = th - aimThPrev[i];
            if (Mathf.Abs(dTh) > 0.4f && Mathf.Abs(dHave) > 0.4f)
                aimSignVote[i] += dHave * dTh > 0f ? 1 : -1;
        }
        aimSignSeen[i] = true;
        aimHavePrev[i] = have;
        aimThPrev[i] = th;
    }

    // World direction the upper arm points, degrees.
    private float UpperArmWorldDeg(int i)
    {
        if (shoulder[i] == null || shoulder[i].attachedRigidbody == null) return 0f;
        Rigidbody2D arm = shoulder[i].attachedRigidbody;
        Vector2 axis = (Vector2)(arm.transform.rotation * Vector3.down);
        return Mathf.Atan2(axis.y, axis.x) * Mathf.Rad2Deg;
    }

    // Centre of the world directions the shoulder can serve: the rest direction
    // (jointAngle 0) the hinge is built around. The reachable window is this
    // +- the joint's range, so an aim outside it is a joint-range question —
    // measured p_arms34 the left arm's window is -18..232 deg world and the
    // right's 110..360, which is why which side is served by which elbow branch
    // is not a mirror question either.
    private float UpperArmRestDeg(int i)
    {
        if (shoulder[i] == null) return 0f;
        return UpperArmWorldDeg(i) - shoulder[i].jointAngle;
    }

    // Joint angle that points the upper arm along a world direction, when that
    // angle exists inside the hinge limits. The world direction of a HingeJoint2D
    // child moves one for one with its joint angle (the joint rotates the child
    // by exactly jointAngle), so the answer is theta + (want - have) — with NO
    // +/-1 sign to guess. The candidate (and its +/-360 representatives, which
    // the wrap needs) is taken if the limits allow it; otherwise the direction is
    // not servable and the caller must pick the other elbow branch.
    private bool JointAngleForDirection(int i, float wantDeg, out float result)
    {
        result = shoulder[i] != null ? shoulder[i].jointAngle : 0f;
        if (shoulder[i] == null) return false;
        float have = UpperArmWorldDeg(i);
        float raw = shoulder[i].jointAngle
            + Mathf.DeltaAngle(have, wantDeg) * Mathf.Clamp01(pressAimGain);
        if (!shoulder[i].useLimits)
        {
            result = raw;
            return true;
        }
        float lo = shoulder[i].limits.min;
        float hi = shoulder[i].limits.max;
        float now = shoulder[i].jointAngle;
        bool found = false;
        float best = Mathf.Clamp(raw, lo, hi);
        for (int k = -1; k <= 1; k++)
        {
            float cand = raw + 360f * k;
            if (cand < lo || cand > hi) continue;
            if (!found || Mathf.Abs(cand - now) < Mathf.Abs(best - now)) best = cand;
            found = true;
        }
        result = best;
        return found;
    }

    // The sign the aim is solved with. It is +1 by construction, not voted: the
    // hinge rotates the child by the joint angle, so d(world direction)/d(theta)
    // is +1 on both sides, mirrored arms included — their difference lives in the
    // rest direction (+142 left against -90 right), not in the derivative.
    private float AimSign(int i)
    {
        return 1f;
    }

    public float PressAimSignL { get { return AimSign(SideL); } }
    public float PressAimSignR { get { return AimSign(SideR); } }
    public float PressAimVoteL { get { return aimSignVote[SideL] >= 0 ? 1f : -1f; } }
    public float PressAimVoteR { get { return aimSignVote[SideR] >= 0 ? 1f : -1f; } }
    // Elbow branch the press is holding: 0 = upper arm at beta+alpha, 1 = beta-alpha,
    // -1 = not solved yet. A press target that flips between the two every step is
    // a posture the PD cannot hold (measured p_arms35 before the branch memory).
    public float PressAimBranchL { get { return aimBranch[SideL]; } }
    public float PressAimBranchR { get { return aimBranch[SideR]; } }

    private float ClampElbow(int i, float target)
    {
        if (elbow[i] != null && elbow[i].useLimits)
            target = Mathf.Clamp(target, elbow[i].limits.min, elbow[i].limits.max);
        return target;
    }

    // ─── muscle write ───

    // ─── the strike (punch cycle p1) ───
    //
    // One stroke = wind-up (base -> the cook) + extension (the cook -> the hit)
    // + return (the hit -> base), all three on the arm chain of ONE side. The
    // strike owns the three joints of that side while it is in flight and the
    // other arm keeps whatever the base asked for; the trunk, the pelvis and
    // both legs are not touched here at all (they belong to BalanceController /
    // the pose library), which is what "a layer above the always-on base" means
    // in this file.
    //
    // Nothing in this method runs unless a request is up or a stroke is in
    // flight: with MotionIntent.punch at 0 the emitted targets are the landed
    // ones bit for bit (R2).
    private void UpdatePunch(float dt)
    {
        if (motionIntent == null) motionIntent = GetComponent<MotionIntent>();
        float request = motionIntent != null ? Mathf.Clamp01(motionIntent.punch) : 0f;
        PunchRequest = request;

        // The press owns the arms while the body is down (the fall is the base's
        // own state, not the strike's): a request cannot fire there and a stroke
        // in flight ends there.
        if (down)
        {
            if (punchClock >= 0f) EndPunch();
            return;
        }

        if (punchClock >= 0f) punchClock += dt;

        // Fire: the first step the request is up fires (the rising edge), and a
        // request still held re-fires once the stroke and the interval are spent
        // (a person jabbing while the key is held).
        float strokeSeconds = PunchStrokeSeconds();
        bool canFire = punchClock < 0f
            || punchClock >= Mathf.Max(strokeSeconds, punchIntervalSeconds);
        if (request > 0.5f && canFire)
        {
            if (punchClock < 0f)
            {
                punchSideIndex = punchSide >= 0f ? SideR : SideL;
                PunchStrikes++;
                PunchLastStrokeSeconds = strokeSeconds;
            }
            punchClock = 0f;
            // The stroke starts from the BASE pose: latch the emission there so
            // the rate cap below has a value to work from on its first step.
            punchEmitShoulder = balance != null ? balance.shoulderBaseAngle : 0f;
            punchEmitElbow = balance != null ? balance.elbowBaseAngle : -20f;
            punchEmitWrist = balance != null ? balance.wristBaseAngle : 0f;
        }

        if (punchClock < 0f)
        {
            PunchPhase = 0;
            PunchAge = 0f;
            return;
        }

        PunchAge = punchClock;
        float shoulderDeg, elbowDeg, wristDeg;
        SampleStrike(out shoulderDeg, out elbowDeg, out wristDeg);

        int side = punchSideIndex;
        // Rate cap on the emitted command (the arm may be mid-swing when the
        // request lands, so the command must not step): the delta is limited per
        // step, the phase clock is not.
        float emit = shoulderDeg;
        float maxStep = Mathf.Max(0f, punchSlewDegPerSecond) * dt;
        if (maxStep > 0f)
            emit = punchEmitShoulder + Mathf.Clamp(shoulderDeg - punchEmitShoulder, -maxStep, maxStep);
        if (dt > 0f)
        {
            float rate = Mathf.Abs(emit - punchEmitShoulder) / dt;
            if (rate > PunchPeakTargetRateDegPerSec) PunchPeakTargetRateDegPerSec = rate;
        }
        punchEmitShoulder = emit;
        punchEmitElbow = elbowDeg;
        punchEmitWrist = wristDeg;

        outShoulder[side] = emit;
        outElbow[side] = elbowDeg;
        outSlew[side] = Mathf.Max(outSlew[side], punchDriveSlewPerSecond);
        if (side == SideL)
        {
            ArmTargetShoulderL = emit;
            ArmTargetElbowL = elbowDeg;
        }
        else
        {
            ArmTargetShoulderR = emit;
            ArmTargetElbowR = elbowDeg;
        }

        if (-emit > PunchPeakShoulderDeg) PunchPeakShoulderDeg = -emit;
        if (-elbowDeg > PunchPeakElbowOpenDeg) PunchPeakElbowOpenDeg = -elbowDeg;
        PunchTargetShoulder = emit;
        PunchTargetElbow = elbowDeg;
        PunchTargetWrist = wristDeg;

        // THE COUNTER-ARM (see punchCounterShare): the other shoulder takes the
        // negative of this side's travel from the base pose - same gains, same
        // ceilings, same slew - so the two arms carry opposite angular momentum
        // and the trunk gets no net impulse to be answered at the ground. The
        // counter elbow stays at the base angle: the shoulder carries the
        // momentum, and one knob is one change.
        if (punchCounterShare > 0.0001f)
        {
            int other = side == SideL ? SideR : SideL;
            float baseS = balance != null ? balance.shoulderBaseAngle : 0f;
            float counterShoulder = baseS - punchCounterShare * (emit - baseS);
            float cap = Mathf.Abs(punchCounterMaxDeg);
            if (cap > 0.0001f)
                counterShoulder = Mathf.Clamp(counterShoulder, baseS - cap, baseS + cap);
            float counterElbow = balance != null ? balance.elbowBaseAngle : -20f;
            outShoulder[other] = counterShoulder;
            outElbow[other] = counterElbow;
            outSlew[other] = Mathf.Max(outSlew[other], punchDriveSlewPerSecond);
            if (other == SideL)
            {
                ArmTargetShoulderL = counterShoulder;
                ArmTargetElbowL = counterElbow;
            }
            else
            {
                ArmTargetShoulderR = counterShoulder;
                ArmTargetElbowR = counterElbow;
            }
        }
    }

    private float PunchStrokeSeconds()
    {
        return Mathf.Max(0f, punchWindSeconds)
            + Mathf.Max(0f, punchExtendSeconds)
            + Mathf.Max(0f, punchReturnSeconds);
    }

    // The three targets of the stroke in force, degrees: base -> the cook -> the
    // hit -> base, smoothstepped inside each phase so the command has no corner
    // of its own (the muscles' own slew and the PD do the rest).
    private void SampleStrike(out float shoulderDeg, out float elbowDeg, out float wristDeg)
    {
        float baseShoulder = balance != null ? balance.shoulderBaseAngle : 0f;
        float baseElbow = balance != null ? balance.elbowBaseAngle : -20f;
        float baseWrist = balance != null ? balance.wristBaseAngle : 0f;

        float wind = Mathf.Max(0.0001f, punchWindSeconds);
        float extend = Mathf.Max(0.0001f, punchExtendSeconds);
        float back = Mathf.Max(0.0001f, punchReturnSeconds);
        float t = punchClock;
        if (t < wind)
        {
            float u = SmoothStep(t / wind);
            PunchPhase = 1;
            shoulderDeg = Mathf.Lerp(baseShoulder, punchShoulderBackDeg, u);
            elbowDeg = Mathf.Lerp(baseElbow, punchElbowBackDeg, u);
            wristDeg = Mathf.Lerp(baseWrist, punchWristBackDeg, u);
        }
        else if (t < wind + extend)
        {
            float u = SmoothStep((t - wind) / extend);
            PunchPhase = 2;
            shoulderDeg = Mathf.Lerp(punchShoulderBackDeg, punchShoulderHitDeg, u);
            elbowDeg = Mathf.Lerp(punchElbowBackDeg, punchElbowHitDeg, u);
            wristDeg = Mathf.Lerp(punchWristBackDeg, punchWristHitDeg, u);
        }
        else
        {
            float u = Mathf.Clamp01((t - wind - extend) / back);
            PunchPhase = u >= 1f ? 0 : 3;
            shoulderDeg = Mathf.Lerp(punchShoulderHitDeg, baseShoulder, SmoothStep(u));
            elbowDeg = Mathf.Lerp(punchElbowHitDeg, baseElbow, SmoothStep(u));
            wristDeg = Mathf.Lerp(punchWristHitDeg, baseWrist, SmoothStep(u));
        }
    }

    private static float SmoothStep(float u)
    {
        u = Mathf.Clamp01(u);
        return u * u * (3f - 2f * u);
    }

    // The stroke ends: no target is written any more, so the next step's
    // emission is the base's own again.
    private void EndPunch()
    {
        punchClock = -1f;
        PunchPhase = 0;
        PunchAge = 0f;
    }

    private bool PunchActive { get { return punchClock >= 0f; } }

    // The striking side's own joints (the wrist takeover is scoped to it).
    private bool PunchSideOwns(int side)
    {
        return punchClock >= 0f && side == punchSideIndex;
    }

    // Every joint the strike is writing this step: the striking side, and - while
    // the counter-arm mirror is on - the other side's shoulder too, which needs
    // the same gains and ceilings to follow its own copy of the command.
    private bool PunchOwns(int side)
    {
        if (punchClock < 0f) return false;
        return side == punchSideIndex || punchCounterShare > 0.0001f;
    }

    private bool SlapOwns(int side)
    {
        return slapClock >= 0f;
    }

    // --- the get-up mat slap (cycle 112) ---
    //
    // The owner's idea (02.10): the heavy torso is peeled off the deck by the
    // reaction of a fast downward arm strike on the mat. The stroke: both
    // palms rise getUpSlapLiftMeters off their contact point, then drive back
    // onto the slab eased-in (u^2, fastest at the hit); it fires on the ENTRY
    // of every get-up attempt (the attempt counter's rising edge) and hands
    // the arms back to the press when it ends. It writes ONLY the two arms'
    // targets while it is in flight, like the punch layer - the trunk, the
    // pelvis and the legs keep their own writers. Lift 0 (default): nothing
    // here runs and every emitted target is the landed one bit for bit.
    private void UpdateGetUpSlap(float dt)
    {
        if (getUpSlapLiftMeters <= 0.001f)
        {
            if (slapClock >= 0f) EndSlap(false);
            return;
        }

        // CYCLE 113: the quiet clock - seconds both settle signals have held
        // under their ceilings. It runs on the same step as the fire check so
        // the edge decision reads a settled run, not one frame of it; a loud
        // frame (a fall, a rock, the entry's own kick) zeroes it.
        if (getUpSlapSettleDeg > 0f || getUpSlapSettleMps > 0f)
        {
            float av = vestibular != null ? vestibular.GetBodyAngularVelocity() : 0f;
            float vy = vestibular != null ? vestibular.GetPelvisVerticalVelocity() : 0f;
            bool loud = (getUpSlapSettleDeg > 0f && Mathf.Abs(av) > getUpSlapSettleDeg)
                || (getUpSlapSettleMps > 0f && Mathf.Abs(vy) > getUpSlapSettleMps);
            if (loud) slapQuietAge = 0f;
            else slapQuietAge += dt;
        }

        // Fire on the rising edge of the attempt counter, while the body is
        // down (that is where an attempt lives), no stroke is in flight, and
        // (cycle 113) the body is SETTLED - the fall-time fire fix.
        int attempts = balance != null ? balance.getUpAttempts : 0;
        if (attempts != slapAttemptsSeen)
        {
            if (attempts > slapAttemptsSeen && down && slapClock < 0f)
            {
                if (SlapSettled()) FireSlap();
                else
                {
                    SlapBlocks++;
                    float av = vestibular != null ? vestibular.GetBodyAngularVelocity() : 0f;
                    float vy = vestibular != null ? vestibular.GetPelvisVerticalVelocity() : 0f;
                    Debug.Log("GETUP-SLAP block t=" + Time.time.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)
                        + " n=" + SlapBlocks
                        + " quiet=" + slapQuietAge.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)
                        + " av=" + av.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture)
                        + " vy=" + vy.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture));
                }
            }
            slapAttemptsSeen = attempts;
        }

        // A body that got up mid-stroke ends the stroke; the press takes back.
        if (slapClock >= 0f && !down) EndSlap(false);
        if (slapClock < 0f) return;

        slapClock += dt;
        float liftEnd = Mathf.Max(0.001f, getUpSlapLiftSeconds);
        float driveEnd = liftEnd + Mathf.Max(0.001f, getUpSlapDriveSeconds);

        for (int i = 0; i < 2; i++)
        {
            float y;
            if (slapClock < liftEnd)
            {
                // The lift: calm, smoothstepped - the wind before the strike.
                float u = slapClock / liftEnd;
                y = Mathf.Lerp(slapAnchorY[i], slapLiftY[i], Mathf.SmoothStep(0f, 1f, u));
            }
            else if (slapClock < driveEnd)
            {
                // The strike: eased IN (u^2), so the palm is at its fastest the
                // moment it reaches the slab - the impulse profile the owner
                // asked for, not a uniform wave.
                float u = (slapClock - liftEnd) / Mathf.Max(0.001f, driveEnd - liftEnd);
                y = Mathf.Lerp(slapLiftY[i], slapAnchorY[i], u * u);
            }
            else
            {
                y = slapAnchorY[i];
            }

            Vector2 point = new Vector2(slapAnchorX[i], y);
            float shTarget, elTarget;
            SolveArm(i, point, out shTarget, out elTarget);
            outShoulder[i] = shTarget;
            outElbow[i] = elTarget;
            outSlew[i] = Mathf.Max(outSlew[i], getUpSlapDriveSlewPerSecond);
            if (i == SideL)
            {
                ArmTargetShoulderL = shTarget;
                ArmTargetElbowL = elTarget;
            }
            else
            {
                ArmTargetShoulderR = shTarget;
                ArmTargetElbowR = elTarget;
            }

            // The palm's own speed - the strike's rate number.
            if (handCol[i] != null)
            {
                float palmY = handCol[i].bounds.center.y;
                if (slapPrevYSeen[i] && dt > 0f)
                {
                    float vy = (palmY - slapPrevY[i]) / dt;
                    if (-vy > SlapPeakDownMps) SlapPeakDownMps = -vy;
                }
                slapPrevY[i] = palmY;
                slapPrevYSeen[i] = true;
            }
        }

        if (!slapLanded && slapClock >= driveEnd)
        {
            slapLanded = true;
            Debug.Log("GETUP-SLAP land t=" + Time.time.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)
                + " peakDown=" + SlapPeakDownMps.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)
                + " contactL=" + (HandContactL ? 1 : 0) + " contactR=" + (HandContactR ? 1 : 0));
        }

        if (slapClock >= driveEnd + Mathf.Max(0f, getUpSlapSettleSeconds)) EndSlap(true);
    }

    private void FireSlap()
    {
        for (int i = 0; i < 2; i++)
        {
            float x = hand[i] != null ? hand[i].position.x : ShoulderPivotWorld(i).x;
            if (handCol[i] != null) x = handCol[i].bounds.center.x;
            slapAnchorX[i] = x;
            slapAnchorY[i] = PalmCentreHeight(i);
            slapLiftY[i] = slapAnchorY[i] + Mathf.Clamp(getUpSlapLiftMeters, 0.02f, 0.6f);
            slapPrevYSeen[i] = false;
        }
        slapClock = 0f;
        slapLanded = false;
        SlapFires++;
        Debug.Log("GETUP-SLAP fire t=" + Time.time.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)
            + " n=" + SlapFires
            + " lift=" + getUpSlapLiftMeters.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture));
    }

    private void EndSlap(bool completed)
    {
        slapClock = -1f;
        SlapEnds++;
        Debug.Log("GETUP-SLAP end t=" + Time.time.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)
            + " completed=" + (completed ? 1 : 0)
            + " peakDown=" + SlapPeakDownMps.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture));
    }

    // CYCLE 113: settled = the quiet run has reached its dwell. Both ceilings
    // 0 = the gate is off (the c112 fire rule, byte for byte).
    private bool SlapSettled()
    {
        if (getUpSlapSettleDeg <= 0f && getUpSlapSettleMps <= 0f) return true;
        return slapQuietAge >= Mathf.Max(0f, getUpSlapQuietSeconds);
    }

    private void WriteMuscles()
    {
        bool pressing = pushEnabled > 0.5f && down && !ReflexOwnsArms;
        for (int i = 0; i < 2; i++)
        {
            bool armPress = pressing && phase[i] != PhaseIdle;
            float pScale = armPress ? Mathf.Max(1f, pressPGainScale) : 1f;
            if (walkBlend > 0.001f && !armPress)
                pScale = Mathf.Max(pScale, walkPGainScale);
            // The strike owns the striking side's gain for its length: same
            // lever class, and off unless a stroke is in flight (R2).
            if (PunchOwns(i))
                pScale = Mathf.Max(pScale, punchPGainScale);
            // CYCLE 112: same lever class for the get-up's mat slap while its
            // stroke is in flight (off unless the knob is armed).
            if (SlapOwns(i))
                pScale = Mathf.Max(pScale, getUpSlapPGainScale);

            float shP = (balance != null ? balance.shoulderPGain : 1.5f) * pScale;
            float shD = balance != null ? balance.shoulderDGain : 0.4f;
            float elP = (balance != null ? balance.elbowPGain : 1.2f) * pScale;
            float elD = balance != null ? balance.elbowDGain : 0.4f;
            float shRef = balance != null ? balance.shoulderErrorReferenceDegrees : 25f;
            float elRef = balance != null ? balance.elbowErrorReferenceDegrees : 25f;

            // CYCLE 90: the arm raise (the owner's gesture, 28.09). While the pose layer's
            // raise is live, the RIGHT arm - and only the right, only in a stand, only off
            // a fall - is driven to the two targets that layer computed on its own clock
            // (one writer per joint: the pose computes them, this writer emits them).
            // armRaise is 0 on every other take, so the stand, the walk and the fall arms
            // stay bit for bit.
            float shCommand = outShoulder[i];
            float elCommand = outElbow[i];
            if (ReflexOwnsArms)
            {
                // The fall reflex's brace: both shoulders forward-down, elbows part open.
                shCommand = reflexShoulderDeg;
                elCommand = reflexElbowDeg;
            }
            else if (i == SideR && !down && walkBlend <= 0.001f
                && balance != null && balance.ArmRaiseActive)
            {
                shCommand = balance.ArmRaiseShoulderTarget;
                elCommand = balance.ArmRaiseElbowTarget;
            }
            Drive(shoulder[i], shCommand, shP, shD, shRef,
                shFlex[i], shExt[i], ref shoulderInertia[i], outSlew[i], shTorqueBase);
            Drive(elbow[i], elCommand, elP, elD, elRef,
                elFlex[i], elExt[i], ref elbowInertia[i], outSlew[i], elTorqueBase);
            // The press loads the palm through the ELBOW; the hand's own pair is
            // the only thing holding that contact, and it is 12x weaker (10 N.m
            // against the elbow's boosted 120). Driven here, while the press owns
            // this side, with the same stable PD and the same base-ceiling damper
            // as the other two joints: a boosted ceiling must buy authority, never
            // damping (arms cycle 11). The target is the hand's own angle at the
            // latch, so this holds the contact patch, it does not aim the arm.
            if (armPress && pressWristHold > 0.5f && wrist[i] != null)
            {
                float wrP = (balance != null ? balance.wristPGain : 1f) * pScale;
                float wrD = (balance != null ? balance.wristDGain : 0.4f);
                float wrRef = (balance != null ? balance.wristErrorReferenceDegrees : 25f);
                Drive(wrist[i], wristHold[i], wrP, wrD, wrRef,
                    wrFlex[i], wrExt[i], ref wristInertia[i],
                    pressElbowSlewPerSecond, wrTorqueBase);
            }
            // The strike takes the STRIKING side's hand for its length: the wrist
            // is the chain's last link and the walk/stand path leaves it at the
            // base angle, so a strike that never wrote it would read as a
            // shoulder-and-elbow move only. Same stable PD, same base-ceiling
            // damper, the strike's own activation slew.
            else if (PunchSideOwns(i) && wrist[i] != null)
            {
                float wrP = (balance != null ? balance.wristPGain : 1f) * pScale;
                float wrD = (balance != null ? balance.wristDGain : 0.4f);
                float wrRef = (balance != null ? balance.wristErrorReferenceDegrees : 25f);
                Drive(wrist[i], punchEmitWrist, wrP, wrD, wrRef,
                    wrFlex[i], wrExt[i], ref wristInertia[i],
                    punchDriveSlewPerSecond, wrTorqueBase);
            }
        }

        // A press stroke needs authority over the palm's friction; outside the
        // press the muscles go back to their own ceilings. Walk cycle 29: while
        // the press owns the arms the scale is at least pressAfterfallTorqueScale
        // (the walk round's afterfall floor; the walk itself never presses).
        float scale = pressing
            ? Mathf.Max(1f, Mathf.Max(pressTorqueScale, pressAfterfallTorqueScale))
            : 1f;
        for (int i = 0; i < 2; i++)
        {
            // The strike's own authority is PER SIDE and only while a stroke is in
            // flight: the same law as the press's boost (the damper stays at the
            // base ceiling, see Drive), and nothing at all when off (R2).
            float sideScale = scale;
            if (PunchOwns(i))
                sideScale = Mathf.Max(sideScale, punchTorqueScale);
            SetPair(shFlex[i], shExt[i], shTorqueBase * sideScale);
            SetPair(elFlex[i], elExt[i], elTorqueBase * sideScale);
            // The hand gets the same authority while the press is on, and its own
            // base ceiling when it is not. Nothing else in the tree writes a
            // muscle's maxTorque at run time (Human.cs sets it at build, Bind reads
            // it back), so restoring the base is exact and the walk is untouched.
            if (pressWristHold > 0.5f)
                SetPair(wrFlex[i], wrExt[i], wrTorqueBase * sideScale);
        }
    }

    private static void SetPair(Muscle flexor, Muscle extensor, float torque)
    {
        if (flexor != null) flexor.maxTorque = torque;
        if (extensor != null) extensor.maxTorque = torque;
    }

    // Stable PD, the same algebra BalanceController uses: the error is taken from
    // the next step and the signal is divided by (1 + K*dt/I), so a strong damper
    // cannot overshoot at the arm's small inertia.
    private void Drive(HingeJoint2D joint, float targetAngle, float pGain, float dGain,
                       float errorRef, Muscle flexor, Muscle extensor,
                       ref float cachedInertia, float slewPerSecond, float damperBase)
    {
        if (joint == null || flexor == null || extensor == null) return;

        if (joint.useLimits)
            targetAngle = Mathf.Clamp(targetAngle, joint.limits.min, joint.limits.max);

        float dt = Time.fixedDeltaTime;
        float speedRef = balance != null ? balance.speedReferenceDegPerSec : 200f;
        float angleForError = joint.jointAngle + joint.jointSpeed * dt;

        float error = Mathf.DeltaAngle(angleForError, targetAngle) / Mathf.Max(1f, errorRef);
        float speed = joint.jointSpeed / Mathf.Max(1f, speedRef);
        float raw = pGain * error - dGain * speed;

        float damping = 0f;
        if (cachedInertia <= 0f)
            cachedInertia = EffectiveInertia(joint);
        if (cachedInertia > 0f)
        {
            // Arm cycle 11: the damper is tuned at the joint's BASE ceiling. The
            // press boost (pressTorqueScale) must buy authority only -- if the
            // damper follows the boost, signal = raw / (1 + 4x damping) and the
            // press throttles the very activation it needs (see pressDamperFromBase).
            float damperTorque = pressDamperFromBase > 0.5f ? damperBase : flexor.maxTorque;
            float k = dGain * damperTorque * Mathf.Rad2Deg / Mathf.Max(1f, speedRef);
            damping = k * dt / cachedInertia;
        }

        float signal = Mathf.Clamp(raw / (1f + damping), -1f, 1f);
        float speedPerSecond = Mathf.Max(20f, slewPerSecond);
        if (signal > 0f)
        {
            Slew(extensor, signal, speedPerSecond);
            Slew(flexor, 0f, speedPerSecond);
        }
        else
        {
            Slew(extensor, 0f, speedPerSecond);
            Slew(flexor, -signal, speedPerSecond);
        }
    }

    private static void Slew(Muscle muscle, float target, float speedPerSecond)
    {
        if (muscle == null) return;
        muscle.activation = Mathf.MoveTowards(
            muscle.activation, target, speedPerSecond * Time.fixedDeltaTime);
    }

    private static float EffectiveInertia(HingeJoint2D joint)
    {
        if (joint == null) return 0f;
        Rigidbody2D self = joint.attachedRigidbody;
        Rigidbody2D connected = joint.connectedBody;
        float a = self != null ? self.inertia : 0f;
        float b = connected != null ? connected.inertia : 0f;
        if (a <= 0f) return b;
        if (b <= 0f) return a;
        return 1f / (1f / a + 1f / b);
    }

    // Real contact vs the geometric band, kept apart on purpose: "the palm is at
    // the floor" is not "the palm grips the floor", and that difference was the
    // whole diagnosis of the last run.
    private void SampleHandContact()
    {
        for (int i = 0; i < 2; i++)
        {
            bool real = false;
            bool near = false;
            Collider2D col = handCol[i];
            if (col != null && col.enabled)
            {
                real = col.IsTouchingLayers(GroundLayers.Mask);
                near = col.bounds.min.y <= groundTopY + handNearGroundSlop;
            }
            if (i == SideL)
            {
                HandContactL = real;
                HandNearGroundL = near;
            }
            else
            {
                HandContactR = real;
                HandNearGroundR = near;
            }
        }
    }

    // ─── matched press window ───

    // Starts on the frame the body goes down. `down` is |tilt| >= fallTiltDeg
    // (or a live RequestArmPress), neither of which looks at pushEnabled, so the
    // press-ON and press-OFF runs of one build open this window on the same
    // event, at the same simulation time, and every number read from it is
    // comparable across the two.
    private void BeginPressWindow()
    {
        // One-shot: the FIRST down edge of the run opens the window. A second
        // fall does not restart it, so every published number keeps its meaning
        // as the largest excursion since the body first went down.
        if (winStarted) return;
        winStarted = true;
        winSeconds = 0f;
        winSamples = 0f;
        winContactHits[SideL] = 0f;
        winContactHits[SideR] = 0f;
        winPivotY[SideL] = ShoulderPivotWorld(SideL).y;
        winPivotY[SideR] = ShoulderPivotWorld(SideR).y;
        winPivotLow[SideL] = winPivotY[SideL];
        winPivotLow[SideR] = winPivotY[SideR];
        winRise[SideL] = 0f;
        winRise[SideR] = 0f;
        winPelvisY = pelvisBody != null ? pelvisBody.position.y : 0f;
        winPelvisX = pelvisBody != null ? pelvisBody.position.x : 0f;
        winPelvisLow = winPelvisY;
        PressWindowPelvisRise = 0f;
        PressWindowTravelX = 0f;
    }

    // Monotone maxima inside the window: a body that gets up and falls again
    // reports its largest excursion, which is what "gets up instead of lying"
    // asks about. The rise is the climb out of the window's own LOWEST point --
    // see the note on the properties for why the down frame cannot be the
    // reference (it is 0.85 m above the settled body on this rig).
    private void SamplePressWindow(float dt)
    {
        if (!winStarted) BeginPressWindow();

        winSeconds += dt;
        winSamples += 1f;
        for (int i = 0; i < 2; i++)
        {
            float pivot = ShoulderPivotWorld(i).y;
            if (pivot < winPivotLow[i]) winPivotLow[i] = pivot;
            float rise = pivot - winPivotLow[i];
            if (rise > winRise[i]) winRise[i] = rise;
            bool contact = i == SideL ? HandContactL : HandContactR;
            if (contact) winContactHits[i] += 1f;
        }
        if (pelvisBody != null)
        {
            float pelvisY = pelvisBody.position.y;
            if (pelvisY < winPelvisLow) winPelvisLow = pelvisY;
            float pelvisRise = pelvisY - winPelvisLow;
            if (pelvisRise > PressWindowPelvisRise) PressWindowPelvisRise = pelvisRise;
            float travel = Mathf.Abs(pelvisBody.position.x - winPelvisX);
            if (travel > PressWindowTravelX) PressWindowTravelX = travel;
        }
        PressWindowSeconds = winSeconds;
        PressWindowShoulderRiseL = winRise[SideL];
        PressWindowShoulderRiseR = winRise[SideR];
        if (winSamples > 0f)
        {
            PressWindowContactFractionL = winContactHits[SideL] / winSamples;
            PressWindowContactFractionR = winContactHits[SideR] / winSamples;
        }
    }

    private void ParseArgs()
    {
        string[] args = LauncherArgs.Args();
        walkShoulderSwing = ReadFloat(args, ArgShoulderSwing, walkShoulderSwing);
        walkElbowAmp = ReadFloat(args, ArgElbowAmp, walkElbowAmp);
        walkElbowMid = ReadFloat(args, ArgElbowMid, walkElbowMid);
        walkElbowLeadSeconds = ReadFloat(args, ArgElbowLead, walkElbowLeadSeconds);
        walkElbowFloorDeg = ReadFloat(args, ArgElbowFloor, walkElbowFloorDeg);
        walkElbowCeilDeg = ReadFloat(args, ArgElbowCeil, walkElbowCeilDeg);
        walkElbowBandKneeDeg = ReadFloat(args, ArgElbowKnee, walkElbowBandKneeDeg);
        swingSign = ReadFloat(args, ArgSign, swingSign);
        ipsWeight = ReadFloat(args, ArgIps, ipsWeight);
        crossWeight = ReadFloat(args, ArgCross, crossWeight);
        crossWeightR = ReadFloat(args, ArgCrossR, crossWeightR);
        walkPGainScale = ReadFloat(args, ArgWalkGain, walkPGainScale);
        pushEnabled = ReadFloat(args, ArgPush, pushEnabled);
        pressTorqueScale = ReadFloat(args, ArgPushScale, pressTorqueScale);
        pressAfterfallTorqueScale = ReadFloat(args, ArgAfterfallScale, pressAfterfallTorqueScale);
        pressDamperFromBase = ReadFloat(args, ArgDamperBase, pressDamperFromBase);
        pressWristHold = ReadFloat(args, ArgWristHold, pressWristHold);
        fallTiltDeg = ReadFloat(args, ArgFallTilt, fallTiltDeg);
        plantSideMax = ReadFloat(args, ArgPlantSide, plantSideMax);
        plantLateralShare = ReadFloat(args, ArgPlantShare, plantLateralShare);
        plantStopMarginDeg = ReadFloat(args, ArgPlantStop, plantStopMarginDeg);
        plantReachMargin = ReadFloat(args, ArgPlantReach, plantReachMargin);
        plantHighLatch = ReadFloat(args, ArgHighLatch, plantHighLatch);
        pressDepth = ReadFloat(args, ArgPressDepth, pressDepth);
        pressSinkCap = ReadFloat(args, ArgSinkCap, pressSinkCap);
        pressStrokeMode = ReadFloat(args, ArgPressMode, pressStrokeMode);
        pressDepthExtra = ReadFloat(args, ArgDepthExtra, pressDepthExtra);
        pressDepthRate = ReadFloat(args, ArgDepthRate, pressDepthRate);
        pressPalmLiveDepth = ReadFloat(args, ArgPalmLive, pressPalmLiveDepth);
        pressTwoPalm = ReadFloat(args, ArgTwoPalm, pressTwoPalm);
        hipStatsTau = ReadFloat(args, ArgHipTau, hipStatsTau);
        walkSwingLowPassLHz = ReadFloat(args, ArgSwingLowPassL, walkSwingLowPassLHz);
        walkSwingLowPassRHz = ReadFloat(args, ArgSwingLowPassR, walkSwingLowPassRHz);
        walkSwingPeakDb = ReadFloat(args, ArgSwingPeakDb, walkSwingPeakDb);
        armSwingMirror = ReadFloat(args, ArgSwingMirror, armSwingMirror);
        if (armSwingMirror > 0.5f && (HasFlag(args, ArgSwingLowPassR) || HasFlag(args, ArgSwingPeakDb)
            || HasFlag(args, ArgCrossR)))
            Debug.Log("ARM MIRROR: -armSwingLpR / -armSwingPeakDb / -armCrossR are ignored (the right side mirrors the left)");
        walkShoulderSlewPerSecond = ReadFloat(args, ArgSwingSlew, walkShoulderSlewPerSecond);
        walkElbowSlewPerSecond = ReadFloat(args, ArgSwingElbowSlew, walkElbowSlewPerSecond);
        pressStrokeDegPerSecond = ReadFloat(args, ArgStrokeRate, pressStrokeDegPerSecond);
        pressMinStrokeSeconds = ReadFloat(args, ArgStrokeMin, pressMinStrokeSeconds);
        pressAimGain = ReadFloat(args, ArgAimGain, pressAimGain);
        pressShoulderBias = ReadFloat(args, ArgPressShoulder, pressShoulderBias);
        pressElbowTarget = ReadFloat(args, ArgPressElbow, pressElbowTarget);
        liftHoldSeconds = ReadFloat(args, ArgLiftHold, liftHoldSeconds);
        liftClearanceMeters = ReadFloat(args, ArgLiftClear, liftClearanceMeters);
        pressElbowSlewPerSecond = ReadFloat(args, ArgElbowSlew, pressElbowSlewPerSecond);
        armDebug = ReadFloat(args, ArgDebug, armDebug);
        punchWindSeconds = ReadFloat(args, ArgPunchWind, punchWindSeconds);
        punchExtendSeconds = ReadFloat(args, ArgPunchExtend, punchExtendSeconds);
        punchReturnSeconds = ReadFloat(args, ArgPunchReturn, punchReturnSeconds);
        punchIntervalSeconds = ReadFloat(args, ArgPunchInterval, punchIntervalSeconds);
        punchShoulderBackDeg = ReadFloat(args, ArgPunchShoulderBack, punchShoulderBackDeg);
        punchShoulderHitDeg = ReadFloat(args, ArgPunchShoulderHit, punchShoulderHitDeg);
        punchElbowBackDeg = ReadFloat(args, ArgPunchElbowBack, punchElbowBackDeg);
        punchElbowHitDeg = ReadFloat(args, ArgPunchElbowHit, punchElbowHitDeg);
        punchWristBackDeg = ReadFloat(args, ArgPunchWristBack, punchWristBackDeg);
        punchWristHitDeg = ReadFloat(args, ArgPunchWristHit, punchWristHitDeg);
        punchSide = ReadFloat(args, ArgPunchSide, punchSide);
        punchTorqueScale = ReadFloat(args, ArgPunchTorque, punchTorqueScale);
        punchPGainScale = ReadFloat(args, ArgPunchGain, punchPGainScale);
        punchSlewDegPerSecond = ReadFloat(args, ArgPunchSlew, punchSlewDegPerSecond);
        punchDriveSlewPerSecond = ReadFloat(args, ArgPunchDriveSlew, punchDriveSlewPerSecond);
        punchCounterShare = ReadFloat(args, ArgPunchCounter, punchCounterShare);
        punchCounterMaxDeg = ReadFloat(args, ArgPunchCounterMax, punchCounterMaxDeg);
        getUpSlapLiftMeters = ReadFloat(args, ArgGetUpSlap, getUpSlapLiftMeters);
        getUpSlapLiftSeconds = ReadFloat(args, ArgGetUpSlapLift, getUpSlapLiftSeconds);
        getUpSlapDriveSeconds = ReadFloat(args, ArgGetUpSlapDrive, getUpSlapDriveSeconds);
        getUpSlapSettleSeconds = ReadFloat(args, ArgGetUpSlapSettle, getUpSlapSettleSeconds);
        getUpSlapDriveSlewPerSecond = ReadFloat(args, ArgGetUpSlapSlew, getUpSlapDriveSlewPerSecond);
        getUpSlapSettleDeg = ReadFloat(args, ArgGetUpSlapSettleDeg, getUpSlapSettleDeg);
        getUpSlapSettleMps = ReadFloat(args, ArgGetUpSlapSettleMps, getUpSlapSettleMps);
        getUpSlapQuietSeconds = ReadFloat(args, ArgGetUpSlapQuiet, getUpSlapQuietSeconds);
    }

    private static bool HasFlag(string[] args, string name)
    {
        for (int i = 0; i < args.Length; i++)
            if (ArgsAudit.Hit(args[i], name))
                return true;
        return false;
    }

    private static float ReadFloat(string[] args, string name, float fallback)
    {
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (!ArgsAudit.Hit(args[i], name)) continue;
            float v;
            if (float.TryParse(args[i + 1], System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out v))
                return v;
        }
        return fallback;
    }
}
