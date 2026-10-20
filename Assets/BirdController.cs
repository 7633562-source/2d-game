using UnityEngine;

public enum BirdMode
{
    Stand = 0,
    Walk = 1,
    Fly = 2,
    Glide = 3,
    // Rest on a perch: wings folded, does not take off on its own.
    Sit = 4,
    // Stoop on prey: less lift, more thrust. Not a hit.
    Attack = 5,
    // Ground peck: neck dips, beak may Hurt. Not Fly.
    Peck = 6
}

// Bird control. Torques go only through Muscle.ApplySignal.
// Lift comes from BirdFlight on the body while the mode is Fly/Glide.
// 10: after BirdDrive / BirdFlockDrive (−50) write Peck, before BeakStrike (20).
[DefaultExecutionOrder(10)]
public class BirdController : MonoBehaviour
{
    // World off-camera rest. NpcRest writes this. Brain may still write mode.
    [System.NonSerialized]
    public bool energyRest;

    [Header("Mode")]
    // Handshake with agent ai: the brain writes only mode (rule ai.mdc).
    // Do not hand over the lift formula or the flap from here.
    public BirdMode mode = BirdMode.Stand;
    // Stand/demo timer, not the brain. 0 — do not take off on its own (do not abort a landing).
    [Tooltip("After this many seconds Stand switches to Fly by itself. 0 — do not take off.")]
    public float takeoffDelay = 2f;
    // Sagittal turn: not a street left, but the nose toward −X. Do not scale the body.
    [Tooltip("+1 — nose toward +X, −1 — nose toward −X.")]
    public float facing = 1f;
    [Tooltip("Turn around after this many seconds. 0 — do not turn on its own.")]
    public float turnAfter = 0f;
    [Tooltip("Rig without joints: rotate the wings as a picture.")]
    public bool flockRig;
    [Tooltip("Flap phase offset, s. So the flock does not flap in unison.")]
    public float flapPhaseOffset;

    [Header("Flap")]
    [Tooltip("Flap frequency, Hz. A crow is ~5; here a bit higher: the 2D stroke is shorter.")]
    public float flapFrequency = 7f;
    [Tooltip("Airspeed that maps to flapFrequency, m/s. 0 — BirdFlight.cruiseSpeed or 2.2.")]
    public float flapSpeedRef = 0f;
    [Tooltip("Slowest flap as a fraction of flapFrequency, at rest.")]
    [Range(0.35f, 1f)]
    public float flapFreqMinScale = 0.55f;
    [Tooltip("Fastest flap as a fraction of flapFrequency, above flapSpeedRef.")]
    [Range(1f, 1.8f)]
    public float flapFreqMaxScale = 1.28f;
    [Tooltip("Dirt-landing flap as a fraction of flapFrequency. Pad Glide still holds the wing.")]
    [Range(0.2f, 0.85f)]
    public float landFlapScale = 0.42f;
    [Tooltip("Shoulder amplitude around mid-stroke, degrees.")]
    public float flapAmplitude = 48f;
    [Tooltip("Mid-stroke of the shoulder. ~90° — wing along the back (the joint points toward −X).")]
    public float flapMidAngle = 90f;
    [Tooltip("Fraction of the period spent on the downstroke. Below 0.5 — faster downstroke, more lift.")]
    [Range(0.25f, 0.5f)]
    public float downstrokeFraction = 0.38f;
    [Tooltip("Elbow flexion on the upstroke: the wing is shorter, less negative lift.")]
    public float upstrokeElbowFlex = 40f;
    [Tooltip("Wrist feathering on the upstroke, degrees. Edge to the flow.")]
    public float upstrokeWristFeather = 35f;
    [Tooltip("Fraction of shoulder torque on the power stroke. SPD to an angle freezes at 7 Hz (bird_fly2).")]
    [Range(0f, 1f)]
    public float flapStrokeGain = 0.45f;
    [Tooltip("Blend-in of tracking the kinematic shoulder target.")]
    public float flapTrackP = 0.35f;

    [Header("Glide")]
    public float glideShoulder = 95f;
    public float glideElbow = -8f;

    [Header("Leg stance")]
    public float standHipAngle = 0f;
    public float standKneeAngle = 10f;
    public float standAnkleAngle = 0f;
    [Tooltip("World body pitch in stance. 0 — horizontal.")]
    public float standBodyPitch = 0f;
    public float tuckHipAngle = 50f;
    public float tuckKneeAngle = 85f;
    [Tooltip("Attack stoop: drop the feet instead of a Fly belly-tuck. 0 — old tuck.")]
    [Range(0f, 1.5f)]
    public float stoopTalonAmp = 1f;
    [Tooltip("Attack stoop: pin the wings to the body. 0 — old flap-mid / elbow −42.")]
    [Range(0f, 1.5f)]
    public float stoopWingAmp = 1f;
    [Tooltip("Attack stoop: open the bill. 0 — old closed bill.")]
    [Range(0f, 1.5f)]
    public float stoopGapeAmp = 1f;
    [Tooltip("Attack stoop: show the tongue in the gape. 0 — old empty bill.")]
    [Range(0f, 1.5f)]
    public float stoopTongueAmp = 1f;
    [Tooltip("Attack stoop: swing the hen wattle. 0 — old glued wattle.")]
    [Range(0f, 1.5f)]
    public float stoopWattleAmp = 1f;
    [Tooltip("Attack stoop: bulge the gullet with the gape. 0 — old silent neck.")]
    [Range(0f, 1.5f)]
    public float stoopCropAmp = 1f;
    [Tooltip("Attack stoop: cover the pupil (nictitating). 0 — old Fly stare.")]
    [Range(0f, 1.5f)]
    public float stoopNictAmp = 1f;
    [Tooltip("Fly still on dirt: load the legs before liftoff. 0 — stand and flap.")]
    [Range(0f, 1.5f)]
    public float takeoffCrouchAmp = 1f;
    [Tooltip("Fly just left dirt: trail the legs then tuck. 0 — old snap tuck.")]
    [Range(0f, 1.5f)]
    public float takeoffTrailAmp = 1f;
    [Tooltip("Fly dirt/first air: cock the wings then a deep first stroke. 0 — old cruise flap.")]
    [Range(0f, 1.5f)]
    public float takeoffWingAmp = 1f;
    [Tooltip("Fly dirt/first air: fan the tail then stream. 0 — old cruise tail.")]
    [Range(0f, 1.5f)]
    public float takeoffTailAmp = 1f;
    [Tooltip("Fly first air: drop the pupil toward the bill. 0 — old cruise stare.")]
    [Range(0f, 1.5f)]
    public float takeoffEyeAmp = 1f;
    [Tooltip("Fly first air: slit the lid over the dropped pupil. 0 — old cruise stare.")]
    [Range(0f, 1.5f)]
    public float takeoffLidAmp = 1f;
    [Tooltip("Fly first air: part the bill toward the climb. 0 — old cruise shut.")]
    [Range(0f, 1.5f)]
    public float takeoffGapeAmp = 1f;
    [Tooltip("Fly first air: hang the hen wattle on the climb. 0 — old glued takeoff wattle.")]
    [Range(0f, 1.5f)]
    public float takeoffWattleAmp = 1f;
    [Tooltip("Fly first air: nod the hen comb on the climb. 0 — old glued takeoff comb.")]
    [Range(0f, 1.5f)]
    public float takeoffCombAmp = 1f;

    [Header("B-TAKEOFF-2 the leap")]
    [Tooltip("Leg impulse takeoff: crouch, then the legs drive. 0 — old wing-only climb.")]
    [Range(0f, 1.5f)]
    public float leapAmp = 1f;
    [Tooltip("Crouch before the drive, s. The body sinks leapCrouchDepth over it.")]
    public float leapCrouchSeconds = 0.16f;
    [Tooltip("Drive, s. The impulse lands on the drive's first step.")]
    public float leapDriveSeconds = 0.10f;
    [Tooltip("Vertical speed the legs give at the drive, m/s. The leap's own share of vy.")]
    public float leapVy = 2.4f;
    [Tooltip("The body's own sink through the crouch, m. The visible wind-up.")]
    public float leapCrouchDepth = 0.055f;
    [Tooltip("After the drive: the support hold stays aside this long, s.")]
    public float leapSettleSeconds = 0.35f;
    [Header("B-TAKEOFF-2 the cadence")]
    [Tooltip("Takeoff burst: strokes per second for the first flaps off the ground. 0 — the cruise rate.")]
    public float flapTakeoffHz = 6.2f;
    [Tooltip("Takeoff burst: strokes at the middle of the 3…6 band. Seeded jitter ±1 per bird.")]
    public int flapTakeoffStrokes = 5;
    [Tooltip("Dirt-landing flap, Hz. 0 — the old flapFrequency × landFlapScale.")]
    public float flapLandingHz = 2.9f;
    [Header("B-TAKEOFF-2 the brake")]
    [Tooltip("Braking strokes: strokes per second on the way down through the last metre. Slow and deep, not the cruise's rate.")]
    public float brakeStrokeHz = 0.9f;
    [Tooltip("Braking strokes: how many deep beats row the air under the bird (2…3 in nature). Spent count.")]
    public int brakeStrokes = 3;
    [Tooltip("Braking strokes: height above the support where they start, m.")]
    public float brakeFeedHeight = 1.5f;
    [Tooltip("Braking strokes: stroke amplitude, x the cruise flap amplitude.")]
    public float brakeStrokeAmp = 1.35f;
    [Header("B-WING-3 the wing's deployment (the picture)")]
    [Tooltip("Upstroke fold of the elbow, degrees. The old 40° shortened the tip reach by ~4% — invisible; a real fold reads >= 1.6x reach between the spread downstroke and the folded upstroke.")]
    public float deployElbowFold = 65f;
    [Tooltip("Upstroke fold of the wrist (the hand segment), degrees. 0 — the old glued-in-line outer wing.")]
    public float deployWristFold = -85f;
    [Tooltip("The chute's own fold: a deeper cup than the cruise's, so the row reads at 25 fps.")]
    public float deployChuteWrist = -95f;
    [Tooltip("The takeoff burst's sweep, x the cruise flap amplitude. The wide power stroke off the ground.")]
    public float deployBurstAmp = 1.15f;
    [Tooltip("Glide shoulder: the held wing raised out of the body silhouette (fold −90 is along the back; the walk's raised wing read at −50 elbow).")]
    public float deployGlideShoulder = 60f;
    [Tooltip("Glide elbow: the held wing a gentle bend, so the sweep reads a blade and not a kink, degrees.")]
    public float deployGlideElbow = -25f;
    [Header("B-WING-6 the folded pack (the ground fold: along the back)")]
    [Tooltip("Ground-fold ulna, degrees: the forearm folds forward over the humerus so the whole chain lies along the back (the old upright -50/-62 read as a vertical bundle / spring at eye scale).")]
    public float groundFoldUlnaZ = 165f;
    [Tooltip("Ground-fold wrist, degrees: the hand folds back along the ulna, so the fan lies along the back and its tips step toward the tail. 0 — the old in-line hand.")]
    public float groundFoldWristZ = -170f;
    [Tooltip("Ruffle/preen/peck flicks: how far the forearm lifts out of the fold, degrees (the old -16 pair under the old -62 fold: the same 42…48 deg lift, rebased).")]
    public float groundFlickLiftDeg = 46f;
    [Tooltip("Glide-pocket hold, s — the intermittent flap-glide. Jittered per cycle (±this).")]
    public float cruiseGlideHoldJitter = 0.45f;
    [Tooltip("Glide-pocket period, s — jittered per cycle (±this) so no lag dominates the height.")]
    public float cruiseGlidePeriodJitter = 0.9f;
    [Tooltip("Glide flare: drop the feet to plant. 0 — dirt stick-hang / perch tuck.")]
    [Range(0f, 1.5f)]
    public float landGearAmp = 1f;
    [Tooltip("Glide flare: raise and cup the wings. 0 — old held-glide / dirt flap mid.")]
    [Range(0f, 1.5f)]
    public float landWingAmp = 1f;
    [Tooltip("Sit after Fly/Glide/Attack: ruffle then fold. 0 — old snap to Sit.")]
    [Range(0f, 1.5f)]
    public float landSettleAmp = 1f;
    [Tooltip("Sit after Fly/Glide/Attack: drop the pupil toward the plant. 0 — old Sit-cruise stare.")]
    [Range(0f, 1.5f)]
    public float landSettleEyeAmp = 1f;
    [Tooltip("Sit after Fly/Glide/Attack: slit the lid over the dropped pupil. 0 — old Sit-cruise stare.")]
    [Range(0f, 1.5f)]
    public float landSettleLidAmp = 1f;
    [Tooltip("Sit after Fly/Glide/Attack: part the bill toward the plant. 0 — old Sit-cruise shut.")]
    [Range(0f, 1.5f)]
    public float landSettleGapeAmp = 1f;
    [Tooltip("Sit after Fly/Glide/Attack: hitch the tail with the ruffle. 0 — old Sit drop.")]
    [Range(0f, 1.5f)]
    public float landSettleTailAmp = 1f;
    [Tooltip("Sit after Fly/Glide/Attack: hang the hen wattle on the plant. 0 — old glued Sit wattle.")]
    [Range(0f, 1.5f)]
    public float landSettleWattleAmp = 1f;
    [Tooltip("Sit after Fly/Glide/Attack: nod the hen comb on the plant. 0 — old glued Sit comb.")]
    [Range(0f, 1.5f)]
    public float landSettleCombAmp = 1f;
    [Tooltip("Fly/Glide flare: drop the pupil toward the dirt. 0 — old cruise stare.")]
    [Range(0f, 1.5f)]
    public float landEyeAmp = 1f;
    [Tooltip("Fly/Glide flare: slit the lid over the dropped pupil. 0 — old cruise stare.")]
    [Range(0f, 1.5f)]
    public float landLidAmp = 1f;
    [Tooltip("Fly/Glide flare: part the bill toward the plant. 0 — old cruise shut.")]
    [Range(0f, 1.5f)]
    public float landGapeAmp = 1f;
    [Tooltip("Fly/Glide flare: hang the hen wattle on the plant. 0 — old glued flare wattle.")]
    [Range(0f, 1.5f)]
    public float landWattleAmp = 1f;
    [Tooltip("Fly/Glide flare: nod the hen comb on the plant. 0 — old glued flare comb.")]
    [Range(0f, 1.5f)]
    public float landCombAmp = 1f;
    [Tooltip("Fly/Glide dirt swoop: drop the pupil toward the dirt. 0 — old cruise stare.")]
    [Range(0f, 1.5f)]
    public float swoopEyeAmp = 1f;
    [Tooltip("Fly/Glide dirt swoop: slit the lid over the dropped pupil. 0 — old cruise stare.")]
    [Range(0f, 1.5f)]
    public float swoopLidAmp = 1f;
    [Tooltip("Fly/Glide dirt swoop: part the bill toward the dirt. 0 — old cruise shut.")]
    [Range(0f, 1.5f)]
    public float swoopGapeAmp = 1f;
    [Tooltip("Fly/Glide dirt swoop: drop the feet out of the belly-tuck. 0 — old cruise tuck.")]
    [Range(0f, 1.5f)]
    public float swoopTalonAmp = 1f;
    [Tooltip("Fly/Glide dirt swoop: show the tongue in the gape. 0 — old empty bill.")]
    [Range(0f, 1.5f)]
    public float swoopTongueAmp = 1f;
    [Tooltip("Sit idleHop: near foot to the head. 0 — old both-leg bob.")]
    [Range(0f, 1.5f)]
    public float sitScratchAmp = 1f;
    [Tooltip("Sit idleHop scratch: slit the lid over the pupil. 0 — old round stare.")]
    [Range(0f, 1.5f)]
    public float sitScratchLidAmp = 1f;
    [Tooltip("Sit idleHop scratch: drop the pupil toward the foot. 0 — old rest pupil.")]
    [Range(0f, 1.5f)]
    public float sitScratchEyeAmp = 1f;
    [Tooltip("Sit idleHop scratch: flick the far wing for balance. 0 — old Sit fold.")]
    [Range(0f, 1.5f)]
    public float sitScratchWingAmp = 1f;
    [Tooltip("Sit idleHop scratch: part the bill toward the foot. 0 — old shut bill.")]
    [Range(0f, 1.5f)]
    public float sitScratchGapeAmp = 1f;
    [Tooltip("Sit idleHop scratch: hang the hen wattle on the kick. 0 — old glued Sit wattle.")]
    [Range(0f, 1.5f)]
    public float sitScratchWattleAmp = 1f;
    [Tooltip("Sit idleHop scratch: nod the hen comb on the kick. 0 — old glued Sit comb.")]
    [Range(0f, 1.5f)]
    public float sitScratchCombAmp = 1f;
    [Tooltip("Sit idleHop scratch: hitch the tail with the kick. 0 — old Sit drop.")]
    [Range(0f, 1.5f)]
    public float sitScratchTailAmp = 1f;
    [Tooltip("Sit SetFacing: hop-turn then plant. 0 — old instant Look flip.")]
    [Range(0f, 1.5f)]
    public float sitTurnAmp = 1f;
    [Tooltip("Sit hop-turn: shrug both wings. 0 — old Sit fold.")]
    [Range(0f, 1.5f)]
    public float sitTurnWingAmp = 1f;
    [Tooltip("Sit hop-turn: drop the pupil toward the perch. 0 — old rest pupil.")]
    [Range(0f, 1.5f)]
    public float sitTurnEyeAmp = 1f;
    [Tooltip("Sit hop-turn: slit the lid over the dropped pupil. 0 — old round stare.")]
    [Range(0f, 1.5f)]
    public float sitTurnLidAmp = 1f;
    [Tooltip("Sit hop-turn: part the bill toward the perch. 0 — old shut bill.")]
    [Range(0f, 1.5f)]
    public float sitTurnGapeAmp = 1f;
    [Tooltip("Sit hop-turn: hang the hen wattle on the plant. 0 — old glued Sit wattle.")]
    [Range(0f, 1.5f)]
    public float sitTurnWattleAmp = 1f;
    [Tooltip("Sit hop-turn: nod the hen comb on the plant. 0 — old glued Sit comb.")]
    [Range(0f, 1.5f)]
    public float sitTurnCombAmp = 1f;
    [Tooltip("Sit hop-turn: hitch the tail with the plant. 0 — old Sit drop.")]
    [Range(0f, 1.5f)]
    public float sitTurnTailAmp = 1f;
    [Tooltip("Thigh flexion in Sit, picture only. Body physics does not squat separately.")]
    public float sitHipAngle = 28f;
    [Tooltip("Near-wing lift on idlePreen, degrees. Picture of the reach, not a force.")]
    public float preenWingLift = 52f;
    [Tooltip("Night Sit tuck, 0…1. Head under the near wing from DayNightCycle.Night01. 0 — off.")]
    [Range(0f, 1f)]
    public float nightRoostAmp = 1f;
    [Tooltip("Sit/Walk/Peck blink on the Head Visual eye. 0 — hide the eye (old blank head).")]
    [Range(0f, 1.5f)]
    public float sitBlinkAmp = 1f;
    [Tooltip("Sit idleLook: glance the pupil with the neck. 0 — old rest pupil.")]
    [Range(0f, 1.5f)]
    public float sitLookEyeAmp = 1f;
    [Tooltip("Sit idleLook: counter-fan the tail with the neck. 0 — old Sit drop.")]
    [Range(0f, 1.5f)]
    public float sitLookTailAmp = 1f;
    [Tooltip("Sit idleLook: hitch the far wing opposite the neck. 0 — old Sit fold.")]
    [Range(0f, 1.5f)]
    public float sitLookWingAmp = 1f;
    [Tooltip("Sit idleLook: part the bill on look-down. 0 — old shut bill.")]
    [Range(0f, 1.5f)]
    public float sitLookGapeAmp = 1f;
    [Tooltip("Sit idleLook: hang the hen wattle on look-down. 0 — old glued Sit wattle.")]
    [Range(0f, 1.5f)]
    public float sitLookWattleAmp = 1f;
    [Tooltip("Sit idleLook: nod the hen comb on look-down. 0 — old glued Sit comb.")]
    [Range(0f, 1.5f)]
    public float sitLookCombAmp = 1f;
    [Tooltip("Sit idlePreen: slit the lid over the pupil. 0 — old round stare.")]
    [Range(0f, 1.5f)]
    public float sitPreenLidAmp = 1f;
    [Tooltip("Sit idlePreen: part the bill toward the near wing. 0 — old shut bill.")]
    [Range(0f, 1.5f)]
    public float sitPreenGapeAmp = 1f;
    [Tooltip("Sit idlePreen: hang the hen wattle on the nibble. 0 — old glued Sit wattle.")]
    [Range(0f, 1.5f)]
    public float sitPreenWattleAmp = 1f;
    [Tooltip("Sit idlePreen: nod the hen comb on the nibble. 0 — old glued Sit comb.")]
    [Range(0f, 1.5f)]
    public float sitPreenCombAmp = 1f;
    [Tooltip("Sit idlePreen: drop the pupil toward the bill. 0 — old rest pupil.")]
    [Range(0f, 1.5f)]
    public float sitPreenEyeAmp = 1f;
    [Tooltip("Sit idlePreen: drop the tail for the reach. 0 — old Sit drop.")]
    [Range(0f, 1.5f)]
    public float sitPreenTailAmp = 1f;
    [Tooltip("Sit idlePreen: hitch the far wing while the near reaches. 0 — old Sit fold.")]
    [Range(0f, 1.5f)]
    public float sitPreenWingAmp = 1f;
    [Tooltip("After Alarm/Call: glance back and fan the tail on Fly/Walk. 0 — beak-only cry.")]
    [Range(0f, 1.5f)]
    public float alarmLookAmp = 1f;
    [Tooltip("After Alarm/Call: drop the pupil through the glance. 0 — old forage stare.")]
    [Range(0f, 1.5f)]
    public float alarmLookEyeAmp = 1f;
    [Tooltip("After Alarm/Call: slit the lid over the dropped pupil. 0 — old forage stare.")]
    [Range(0f, 1.5f)]
    public float alarmLookLidAmp = 1f;
    [Tooltip("After Alarm/Call: part the bill through the glance after cry. 0 — old forage shut.")]
    [Range(0f, 1.5f)]
    public float alarmLookGapeAmp = 1f;
    [Tooltip("After Alarm/Call: hang the hen wattle through the glance after cry. 0 — old glued glance wattle.")]
    [Range(0f, 1.5f)]
    public float alarmLookWattleAmp = 1f;
    [Tooltip("After Alarm/Call: nod the hen comb through the glance after cry. 0 — old glued glance comb.")]
    [Range(0f, 1.5f)]
    public float alarmLookCombAmp = 1f;
    [Tooltip("After Alarm/Call: pin Fly wings / hitch Walk wings through the glance. 0 — old cruise flap.")]
    [Range(0f, 1.5f)]
    public float alarmLookWingAmp = 1f;
    [Tooltip("After Alarm/Call: scramble Walk legs through the glance. 0 — old forage stride/hop.")]
    [Range(0f, 1.5f)]
    public float alarmLookLegAmp = 1f;
    [Tooltip("After Alarm/Call: freeze Sit look/preen/scratch through the glance. 0 — old forage fidget on Sit.")]
    [Range(0f, 1.5f)]
    public float alarmLookSitAmp = 1f;
    [Tooltip("After Alarm/Call: hitch Sit wings off the fold through the glance. 0 — old Sit fold.")]
    [Range(0f, 1.5f)]
    public float alarmLookSitWingAmp = 1f;
    [Tooltip("After Alarm/Call: crouch Sit legs through the glance. 0 — old Sit plant.")]
    [Range(0f, 1.5f)]
    public float alarmLookSitLegAmp = 1f;
    [Tooltip("After Alarm/Call: clench Sit feet through the glance. 0 — old Sit plant feet.")]
    [Range(0f, 1.5f)]
    public float alarmLookSitGraspAmp = 1f;
    [Tooltip("After Alarm/Call: hunch Sit neck/wings through the glance. 0 — old Sit silhouette.")]
    [Range(0f, 1.5f)]
    public float alarmLookSitHunchAmp = 1f;
    [Tooltip("After Alarm/Call: drop Sit tail through the glance. 0 — old Sit tail.")]
    [Range(0f, 1.5f)]
    public float alarmLookSitTailAmp = 1f;
    [Tooltip("After Alarm/Call: hold Sit stare (skip forage blink) through the glance and hop-turn. 0 — old Sit blink.")]
    [Range(0f, 1.5f)]
    public float alarmLookSitBlinkAmp = 1f;
    [Tooltip("After Alarm/Call: hold Walk stare (skip forage blink) through the glance and hop-turn. 0 — old Walk blink.")]
    [Range(0f, 1.5f)]
    public float alarmLookWalkBlinkAmp = 1f;
    [Tooltip("Fly/Glide SetFacing: bank the wings then cruise. 0 — old instant Look flip.")]
    [Range(0f, 1.5f)]
    public float airTurnAmp = 1f;
    [Tooltip("Fly/Glide SetFacing: hitch the tail with the bank. 0 — old cruise stream.")]
    [Range(0f, 1.5f)]
    public float airTurnTailAmp = 1f;
    [Tooltip("Fly/Glide SetFacing: drop the pupil through the bank. 0 — old cruise stare.")]
    [Range(0f, 1.5f)]
    public float airTurnEyeAmp = 1f;
    [Tooltip("Fly/Glide SetFacing: slit the lid over the dropped pupil. 0 — old cruise stare.")]
    [Range(0f, 1.5f)]
    public float airTurnLidAmp = 1f;
    [Tooltip("Fly/Glide SetFacing: part the bill through the bank. 0 — old cruise shut.")]
    [Range(0f, 1.5f)]
    public float airTurnGapeAmp = 1f;
    [Tooltip("Sit/Peck on a pad → Fly: same first-air trail as leaving dirt. 0 — old instant tuck.")]
    [Range(0f, 1.5f)]
    public float perchTakeoffAmp = 1f;
    [Tooltip("Sit/Peck on a pad: wrap the toes. 0 — old dirt-slab feet.")]
    [Range(0f, 1.5f)]
    public float sitGraspAmp = 1f;
    [Tooltip("Sit/Peck on a pad: crouch the tarsi and drop the tail. 0 — old dirt-leg sit.")]
    [Range(0f, 1.5f)]
    public float sitCrouchAmp = 1f;
    [Tooltip("Sit/Peck on a pad: hunch the neck and tuck the wings. 0 — old dirt-sit silhouette.")]
    [Range(0f, 1.5f)]
    public float sitHunchAmp = 1f;

    [Header("Walk")]
    public float walkFrequency = 2.5f;
    public float walkHipAmp = 22f;
    public float walkKneeAmp = 30f;
    [Tooltip("Walk foot plant, degrees. Stance stays flat; swing toes down. 0 — off.")]
    public float walkFootAmp = 18f;
    // B-PERCH-25 (27.09): THE GROUNDED SETTLE. The plate hold cancels gravity, so a
    // bird that stopped flying rested wherever it was inside the hold's own +0.04 m
    // window — take bPCH24c: bodyY -1.8190 against the -1.8530 stance root (its
    // spawn) — and the drawn feet floated 9 px (0.02 m) over the drawn ground, the
    // fault every phase-8 close read names ("floating … not anchored by its legs to
    // the ground"). The gain draws the grounded body back down to the stance root
    // at (gear x stanceH) m/s; at the root the drawn feet meet the grass. 0 = the
    // old hold (the vy freeze). -birdGroundSettle overrides it per take.
    public float groundSettleGain = 8f;
    [Tooltip("Hen Walk: both legs hop together. 0 — crow alternating stride.")]
    [Range(0f, 1.5f)]
    public float walkHopAmp = 0f;
    [Tooltip("Hen Walk hop: flick both wings on the tuck. 0 — old folded Walk.")]
    [Range(0f, 1.5f)]
    public float walkHopWingAmp = 1f;
    [Tooltip("Hen Walk hop: pump the tail on the tuck. 0 — old Sit drop.")]
    [Range(0f, 1.5f)]
    public float walkHopTailAmp = 1f;
    [Tooltip("Crow Walk stride: hitch the opposite wing. 0 — old Sit-adjacent fold.")]
    [Range(0f, 1.5f)]
    public float walkStrideWingAmp = 1f;
    [Tooltip("Crow Walk stride: pump the tail with each step. 0 — old Sit drop.")]
    [Range(0f, 1.5f)]
    public float walkStrideTailAmp = 1f;
    [Tooltip("Walk forage: dip the neck toward the dirt. 0 — old ahead bob.")]
    [Range(0f, 1.5f)]
    public float walkScanAmp = 1f;
    [Tooltip("Walk forage: drop the pupil toward the dirt. 0 — old rest pupil.")]
    [Range(0f, 1.5f)]
    public float walkEyeAmp = 1f;
    [Tooltip("Walk forage: slit the lid over the dropped pupil. 0 — old round stare.")]
    [Range(0f, 1.5f)]
    public float walkLidAmp = 1f;
    [Tooltip("Walk forage: part the bill toward the dirt. 0 — old shut bill.")]
    [Range(0f, 1.5f)]
    public float walkGapeAmp = 1f;
    [Tooltip("Hen Walk hop: swing the wattle on the tuck. 0 — old glued Walk wattle.")]
    [Range(0f, 1.5f)]
    public float walkWattleAmp = 1f;
    [Tooltip("Hen Walk hop: nod the comb on the tuck. 0 — old glued Walk comb.")]
    [Range(0f, 1.5f)]
    public float walkCombAmp = 1f;
    [Tooltip("Walk SetFacing: plant-hop then resume stride. 0 — old instant Look flip.")]
    [Range(0f, 1.5f)]
    public float walkTurnAmp = 1f;
    [Tooltip("Walk hop-turn: shrug both wings. 0 — old Walk fold.")]
    [Range(0f, 1.5f)]
    public float walkTurnWingAmp = 1f;
    [Tooltip("Walk hop-turn: drop the pupil toward the dirt. 0 — old rest pupil.")]
    [Range(0f, 1.5f)]
    public float walkTurnEyeAmp = 1f;
    [Tooltip("Walk hop-turn: slit the lid over the dropped pupil. 0 — old round stare.")]
    [Range(0f, 1.5f)]
    public float walkTurnLidAmp = 1f;
    [Tooltip("Walk hop-turn: part the bill toward the dirt. 0 — old shut bill.")]
    [Range(0f, 1.5f)]
    public float walkTurnGapeAmp = 1f;
    [Tooltip("Walk hop-turn: hang the hen wattle on the plant. 0 — old glued Walk wattle.")]
    [Range(0f, 1.5f)]
    public float walkTurnWattleAmp = 1f;
    [Tooltip("Walk hop-turn: nod the hen comb on the plant. 0 — old glued Walk comb.")]
    [Range(0f, 1.5f)]
    public float walkTurnCombAmp = 1f;
    [Tooltip("Walk hop-turn: hitch the tail with the plant. 0 — old Sit drop.")]
    [Range(0f, 1.5f)]
    public float walkTurnTailAmp = 1f;
    [Tooltip("Neck nod on Walk, degrees. Picture of the stride, not a force.")]
    public float walkHeadBob = 9f;
    [Tooltip("Flock walk thrust, N. Must exceed perch friction, or the bird stands still.")]
    public float walkThrust = 2.2f;
    [Tooltip("Walk speed cap, m/s. Otherwise it slides off the stand perch.")]
    public float walkSpeed = 0.7f;
    // ---- B-GROUND-1 "The claws hold" (flock, -birdClawGrip) ----
    // The pads themselves live in Bird.cs (BuildClaw). What is here is the
    // plant: which foot of the picture is down, how far the claw is allowed to
    // be dragged on its plant (the pin), and the gait clock — driven by the
    // bird's own travel while the grip is on, so the leg sweep and the body's
    // advance agree and the planted claw has nothing to skate against.
    [Tooltip("The pad's own lowest reach counts as planted within this, m (-birdClawTol).")]
    public float clawPlantTol = 0.0025f;
    [Tooltip("Pin: the most the picture's foot may be corrected per frame, m.")]
    public float clawPinStep = 0.004f;
    [Tooltip("Pin: the most one plant may correct in total, m.")]
    public float clawPinBudget = 0.02f;
    [Tooltip("The gait's own step length, m. 0 = measure the foot's own sweep.")]
    public float clawStrideMeters;
    [Tooltip("Walk speed cap while the grip is on, m/s (-birdClawSpeed).")]
    public float clawWalkSpeed = 0.4f;
    // ---- B-GROUND-2 "stand or walk" (-birdNoSlide) ----
    [Tooltip("Ground governor's dead band, m/s. Nothing is braked inside it.")]
    public float groundGovernorBand = 0.05f;
    [Tooltip("Ground governor's brake, as a share of walkThrust.")]
    public float groundGovernorBrake = 2f;
    public static bool ClawPinOn = true;
    private float clawGaitCycles;
    private float clawLastX;
    private readonly bool[] clawPlanted = new bool[2];
    private readonly float[] clawMinRelY = new float[2];
    private readonly float[] clawPlantX = new float[2];
    private readonly float[] clawPlantRelX = new float[2];
    private readonly float[] clawPinUsed = new float[2];
    private readonly float[] clawSlipMax = new float[2];
    private readonly float[] clawSlipNow = new float[2];
    private readonly Vector3[] clawFootHome = new Vector3[2];
    private float clawStrideObs;
    private readonly float[] clawSwingMin = { float.MaxValue, float.MaxValue };
    private readonly float[] clawSwingMax = { float.MinValue, float.MinValue };
    public float debugClawLX;
    public float debugClawLY;
    public float debugClawRX;
    public float debugClawRY;
    public int debugClawLOn;
    public int debugClawROn;
    public float debugClawSlipL;
    public float debugClawSlipR;
    public float debugClawStride;
    // B-VIS-WALK: the drawn step's own read - the phase the override drew with
    // and its flag (0 = the old picture). Recorder columns, appended last.
    public float debugStepPhase = -1f;
    public int debugStepOn;
    [Tooltip("Enable the perch when height above stance is below this, m.")]
    public float perchApproachHeight = 0.5f;
    [Tooltip("Count liftoff only above this. Otherwise Fly near the ground sits immediately.")]
    public float takeoffClearance = 0.28f;
    [Tooltip("Brain: this Glide is dirt forage, not a pad return. TickFlock must not Sit on a twig.")]
    public bool preferDirtLand;

    [Header("Joint PD")]
    public float jointPGain = 2f;
    public float jointDGain = 0.35f;
    public float jointErrorRef = 25f;
    public float speedReferenceDegPerSec = 100f;

    [Header("Tail pitch")]
    public float pitchPGain = 1.6f;
    public float pitchDGain = 0.35f;
    public float pitchTarget = 8f;
    public float pitchErrorRef = 25f;

    [Header("Head")]
    public float headTargetTilt = 0f;
    [Tooltip("Neck dip on Peck, degrees. Negative is beak toward the dirt.")]
    public float peckDipDegrees = 48f;
    [Tooltip("Beak open on the Peck down-dip, degrees. Picture of the grab, not Hurt.")]
    public float peckGapeAmp = 20f;
    [Tooltip("Peck up-stroke: toss the head to swallow. 0 — stay dipped.")]
    [Range(0f, 1.5f)]
    public float peckSwallowAmp = 1f;
    [Tooltip("Dirt Peck: crouch the tarsi on the jab. 0 — old standing nod.")]
    [Range(0f, 1.5f)]
    public float peckLungeAmp = 1f;
    // B-PERCH-23: the reach picture's FORWARD component, metres (the drop stays 0.08).
    // The offset rides the neck's own anchor, so the whole head plate travels with it:
    // at 0.08 forward the plate clears the body's silhouette at the full dip (measured
    // 0.042 m = ~25 px of background between the body's edge and the head's box) — the
    // eyes' phase-8 fault "the head detaches". The dip's own swing is 0.027 m at 48 deg,
    // so the forward value, not the dip, is the detach's lever.
    [Tooltip("Peck jab: the neck drop's forward component, metres. 0 — the head drops straight down the body's front.")]
    // B-PERCH-23: the peck's forward reach at the stroke. The ladder (the phase-8 strip, the eyes on
    // gemini-3.1-flash-lite): 0.08 (the old const) 3|3|3 - the head detaches; 0.05 4|4|4; 0.02 6|6|6;
    // 0.0 7|6|6 with the fault family MOVED (the head's plate merges with the body on the stroke
    // frames; the residual is the body's own shift). 0.0 ADOPTED (B-PERCH-23 close).
    public float peckReachAhead = 0f;
    // B-PERCH-26: the peck's own vertical drop, metres (the old hard 0.08f in
    // ApplyPeckNeckReachPicture). The drop carries the head's plate down the body's
    // front; the round's ladder on the ground suite (the same cut, the same pin
    // gemini-3.8-flash, the A/B sheet at the stroke frames):
    //   0.00 (bPCH26a) - the head high, the beak points UP: "pecks the air"; the A/B 3
    //   0.04 (bPCH26c) - the head deeper in the body, the beak's tip ~0.15 m
    //   0.08 (the old const, bPCH25a) - the head's box buried in the body, the tip ~0.10-0.12 m; the A/B 3|3|3
    //   0.14 (bPCH26b) - the head leaves the body as its own block, the tip ~0.05 m; the A/B 8|9|9 -> ADOPTED
    //   0.19 (bPCH26d) - the head clears the body entirely and hangs without a neck: "detached"
    // -birdPeckNeckDrop overrides it for a ladder; absent = this default.
    public float peckNeckDropM = 0.14f;
    // B-PERCH-27: the body's own lowering at the jab, metres (picture only). The rig's own
    // crouch is picture-only, so the torso sat pinned at the stance root through the whole
    // bout (bodyY -1.8530, the record) while the strike read as a head hung off a statue.
    // This lowers the drawn torso AND the whole upper body (Look: neck, head, wings, tail)
    // by peckBodyDropM * jab - the same envelope as the neck's drop - over legs that keep
    // their world pose: the thigh roots ride up by the same dose, so the feet and the claws
    // stay planted and no collider, contact or solver value is touched.
    // -birdPeckBodyDrop overrides it for a ladder; absent = this default.
    // Ladder (the ground suite, the arms md5-identical off the stroke): the tip above the
    // grass at t=8.20/8.25/8.30 and the body's own top vs the grass in px:
    //   0.00 (bPCH27g) - tip 0.038/0.044/0.044 m; the body -99/-106/-106 px
    //   0.02 (bPCH27h) - tip 0.018/0.024/0.024;   the body -90/-96/-96  (-10 px)
    //   0.04 (bPCH27i) - tip 0.004/0.007/0.009 THE BEAK REACHES THE GRASS; -81/-88/-89 (-18 px) -> ADOPTED
    //   0.06 (bPCH27j) - the tip clips INTO the grass (0.004/0.004/0.007) and the camAuto's
    //                    refit drifts through 8.35-8.55 (the grass 603 against 592) -> refused
    // The feet stay planted at every dose (the lowest drawn row -2 px off the grass, constant).
    public float peckBodyDropM = 0.04f;
    [Tooltip("Peck jab: flick both wings off the Sit fold. 0 — old tucked Peck.")]
    [Range(0f, 1.5f)]
    public float peckWingAmp = 1f;
    [Tooltip("Peck jab: pump the tail off the Sit drop. 0 — old Sit drop.")]
    [Range(0f, 1.5f)]
    public float peckTailAmp = 1f;
    [Tooltip("Peck jab: slit the lid over the pupil. 0 — old open-eye jab.")]
    [Range(0f, 1.5f)]
    public float peckLidAmp = 1f;
    [Tooltip("Peck forage: drop the pupil toward the dirt. 0 — old rest pupil on swallow.")]
    [Range(0f, 1.5f)]
    public float peckEyeAmp = 1f;
    [Tooltip("Peck swallow: bulge the gullet on the neck. 0 — old silent neck.")]
    [Range(0f, 1.5f)]
    public float peckCropAmp = 1f;
    [Tooltip("Peck swallow: hang the hen wattle on the toss. 0 — old glued Peck wattle.")]
    [Range(0f, 1.5f)]
    public float peckWattleAmp = 1f;
    [Tooltip("Peck swallow: nod the hen comb on the toss. 0 — old glued Peck comb.")]
    [Range(0f, 1.5f)]
    public float peckCombAmp = 1f;
    [Tooltip("Alarm/Call shout: bulge the gullet with the gape. 0 — old silent neck.")]
    [Range(0f, 1.5f)]
    public float cryCropAmp = 1f;
    [Tooltip("Alarm/Call shout: show the tongue in the gape. 0 — old empty bill.")]
    [Range(0f, 1.5f)]
    public float cryTongueAmp = 1f;
    [Tooltip("Alarm/Call shout: swing the hen wattle. 0 — old statue wattle.")]
    [Range(0f, 1.5f)]
    public float cryWattleAmp = 1f;
    [Tooltip("Alarm/Call shout: nod the hen comb. 0 — old glued comb.")]
    [Range(0f, 1.5f)]
    public float cryCombAmp = 1f;
    [Tooltip("Woodpecker Peck hammer, Hz. Closed bill on the trunk. 0 — hen dip.")]
    public float drumHammerHz = 12f;
    [Tooltip("Woodpecker hammer stroke, degrees. Picture only, not a wood force.")]
    public float drumHammerAmp = 10f;
    [Tooltip("Woodpecker neck brace toward the trunk during a drum, degrees.")]
    public float drumRestDip = 18f;

    // Brain writes in Update. Picture only — not a force and not Peck (beak Hurt).
    [System.NonSerialized] public float idleLook;
    [System.NonSerialized] public float idlePreen;
    [System.NonSerialized] public float idleHop;
    [System.NonSerialized] public float playRoll;
    // Crow dirt forage. Brain (BirdWander) writes these; hens stay Cos.
    [System.NonSerialized] public float searchCrouch;
    [System.NonSerialized] public float peckWindup;
    [System.NonSerialized] public float peckStrike;
    [System.NonSerialized] public float peckHeadUp;
    [System.NonSerialized] public bool peckMiss;

    [Header("Ankle from CoM")]
    public float ankleComP = 14f;
    public float ankleComD = 8f;
    public float comOffsetReference = 0.05f;

    [Header("Debug")]
    public float debugLiftY;
    public float debugFlapPhase;
    public bool debugDownstroke;

    public Muscle leftShoulderFlexor, leftShoulderExtensor;
    public Muscle rightShoulderFlexor, rightShoulderExtensor;
    public Muscle leftElbowFlexor, leftElbowExtensor;
    public Muscle rightElbowFlexor, rightElbowExtensor;
    public Muscle leftWristFlexor, leftWristExtensor;
    public Muscle rightWristFlexor, rightWristExtensor;
    public Muscle leftHipFlexor, leftHipExtensor;
    public Muscle rightHipFlexor, rightHipExtensor;
    public Muscle leftKneeFlexor, leftKneeExtensor;
    public Muscle rightKneeFlexor, rightKneeExtensor;
    public Muscle leftAnkleFlexor, leftAnkleExtensor;
    public Muscle rightAnkleFlexor, rightAnkleExtensor;
    public Muscle neckFlexor, neckExtensor;
    public Muscle headFlexor, headExtensor;
    public Muscle beakFlexor, beakExtensor;
    public Muscle tailFlexor, tailExtensor;

    public HingeJoint2D leftShoulderJoint, rightShoulderJoint;
    public HingeJoint2D leftElbowJoint, rightElbowJoint;
    public HingeJoint2D leftWristJoint, rightWristJoint;
    public HingeJoint2D leftHipJoint, rightHipJoint;
    public HingeJoint2D leftKneeJoint, rightKneeJoint;
    public HingeJoint2D leftAnkleJoint, rightAnkleJoint;
    public HingeJoint2D neckJoint, headJoint, beakJoint, tailJoint;

    private BirdSensors sensors;
    private BirdFlight flight;
    private Bird bird;
    private Rigidbody2D bodyRb;
    // The flock reads the body without Find on Bind.
    public Rigidbody2D BodyRb => bodyRb;
    private float standElapsed;
    private bool wasAirborne;
    private float leftShoulderI, rightShoulderI;
    private float leftElbowI, rightElbowI;
    private float leftWristI, rightWristI;
    private float leftHipI, rightHipI;
    private float leftKneeI, rightKneeI;
    private float leftAnkleI, rightAnkleI;
    private float neckI, headI, beakI;
    private float flockWingFold;
    private float flockLeftWingFold;
    private float flockRightWingFold;
    private float flockNeckZ;
    private float flockHeadZ;
    private float flockUlnaZ;
    private float flockLeftUlnaZ;
    private float flockRightUlnaZ;
    // B-WING-3: the hand (wrist) of the picture. It used to be glued in line
    // with the ulna; the deployment rounds the outer wing here.
    private float flockWristZ;
    private float flockLeftWristZ;
    private float flockRightWristZ;
    // B-WING-4: the fan's own drive. The spread phases open the feather fan
    // (the burst's downstroke, the held glide pocket, the glide, the chute's
    // downstroke); every upstroke and every ground pose keeps it folded.
    private float flockFeatherTarget;
    private float flockFeatherOpen;
    private float flockLeftThighZ;
    private float flockRightThighZ;
    private float flockLeftShankZ;
    private float flockRightShankZ;
    private float flockLeftFootZ;
    private float flockRightFootZ;
    private float lastLeftShankZ = 999f;
    private float lastRightShankZ = 999f;
    private float lastLeftFootZ = 999f;
    private float lastRightFootZ = 999f;
    private bool didAutoTurn;
    private float flapCycle;
    private bool flapCycleSeeded;
    // B-TAKEOFF-2 the leap: the crouch's and the drive's own clock (one int
    // phase), the cooldown the edge needs so a mode flicker cannot re-leap,
    // and the numbers the ruler reads (debugLeapVy, debugLeapAt, leaps).
    private int leapPhase;
    private float leapPhaseUntil = -1f;
    private float leapCoolUntil = -1f;
    private float leapCrouchBorn;
    // B-TAKEOFF-2 the cadence: the takeoff burst's own countdown. The phase
    // wrap spends one stroke; FlapFrequencyNow reads the countdown.
    private int takeoffStrokesLeft;
    // B-TAKEOFF-2 the brake: the deep strokes on the way down. Armed again
    // whenever the bird is up above brakeFeedHeight, spent on the descent.
    private int brakeStrokesLeft;
    public int debugBrakesFired;
    public bool debugBrakeOn;
    // B-WING-3: the picture's own parachute window (the flock pose's brake row).
    // The physics' debugBrakeOn stays the physics' own read.
    public bool debugChuteOn;
    private int takeoffBurstStrokes;
    [System.NonSerialized] public float debugFlapHz;
    [System.NonSerialized] public float debugGlideBlend;
    [System.NonSerialized] public int takeoffBursts;
    [System.NonSerialized] public float debugLeapVy;
    [System.NonSerialized] public float debugLeapAt = -1f;
    [System.NonSerialized] public int leaps;
    [System.NonSerialized] public int leapRefusals;
    private BirdPerch perchPad;
    // B-PERCH-11 (26.09): THE SETTLE WINDOW. The ring trigger opens it on the
    // seat: while it is open the step state reads Sit, so the pad survives the
    // drive's per-frame fly flicker (bPCH10b: mode=2 at every step from the
    // seat on, the bird lifted 0.06 s after the touch) and the sensors hand
    // the wander its own Sit sync (bPCH3d: mode=4 across the hold).
    [System.NonSerialized] public float perchSettleUntil;
    [System.NonSerialized] public int dbgSettleHold;
    private BirdPerch offeredPerch;
    // B-PERCH-9: the ring trigger's arrival-window offer (the hold pose) is
    // remembered apart from the pad's own trigger offer: it was made at the
    // seat pose itself, so the glide attach may take it even when the air
    // latch never rose (the crown arrival: the feet kept reading support, so
    // wasAirborne stayed false and the offer was dropped). Debug counters
    // ride the same round and land in the trigger's summary.
    private bool offeredPerchHold;
    public int dbgOfferSeen;
    public int dbgLandRefused;
    public int dbgLandBound;
    // B-PERCH-9 probes (inert): which non-flight branch saw or ate the offer.
    public int dbgSitOffer;
    public int dbgSitAttach;
    public int dbgHuntClear;
    private Vector2 perchHoldPos;
    private Vector3 perchHoldLocal;
    private readonly Collider2D[] perchHits = new Collider2D[24];
    private float lastLookRoll;
    private float flockBeakZ;
    // Peck/Attack dirt eat: rest pose of the beak trigger. Hunt Peck
    // holds that trigger on the dirt slab for the whole Peck, not only
    // the Cos jab. BeakStrike still gates Hurt on Rel Hostile.
    private Vector2 beakRestOffset;
    private Vector2 beakRestSize;
    private bool beakRestLatched;
    private bool huntBeakSlabLive;
    // Keep HuntBeak live across preferDirtLand flicker / leftover
    // treeSupport. Decrement only once per FixedUpdate (Wants used
    // to spend the latch before Simulate, then Rel Hostile missed).
    private bool huntEatLatch;
    private int huntEatHoldFixed;
    // Dirt eat query: world-root Default BoxCollider2D plus a
    // Rigidbody2D. Parenting to Body made OverlapCollider use BirdCore.
    // The project matrix ignores Core×Default (Insects spawn on
    // Default), so Rel Hostile never reached Hurt. A Default fixture
    // with no attached body is silent (14:33). Static+simulated still
    // returns 0 hits: Box2D never builds Static×Static. Two kinematic
    // sensors (HuntBeak trigger × Insect trigger) built no pair —
    // useFullKinematicContacts still left OverlapCollider empty, so
    // Rel Hostile never Hurt. Insect is a FreezeAll kinematic trigger
    // with the flag and NeverSleep. Two triggers (HuntBeak × Insect)
    // still built no pair even when HuntBeak was Dynamic — BeakStrike
    // OverlapCollider never Rel Hostile. Live eat is a Dynamic solid
    // × that Insect trigger (the pair OverlapCollider can see). A
    // live Dynamic Default body on preferDirtLand Walk (8 m close,
    // DirtBout forage) was the flock 200 Hz island — park unsim on
    // Walk, Dynamic NeverSleep only while Peck/Attack. FreezeRotation
    // so Rigidbody2D.position can track body X — FreezeAll on a live
    // query pinned the AABB at the origin / ParkY. gravity 0,
    // Discrete. Pose is Rigidbody2D.position (physics AABB), offset 0
    // while live. Strip is body-X centred with PeckHold both ways —
    // leftover SetFacing used to teleport a facing-biased AABB off
    // Rel Hostile. Writing ParkY offset while simulated baked y=80
    // into the Box2D fixture; simulated=true before clearing that
    // offset left OverlapCollider at dirt+80, so Rel Hostile never
    // reached Hurt. Wake drops FreezeAll, poses offset 0 at dirt
    // while unsim, then simulated=true so the fixture bakes on the
    // insect film. simulated only while the slab is live — park
    // unsims so leftover Sit / hunt Walk leaves Physics2D.Simulate
    // (NpcRest rest does the same; not a leftover 200 Hz island).
    // Park offset (0, ParkY) is the NpcRest rest signal and is
    // written only after unsim. Park is Kinematic StartAwake so a
    // stray simulated=true at ParkY is not a NeverSleep island.
    // Collider stays enabled (disable was the 1×1 origin miss). No
    // GetContacts. No SyncTransforms.
    private BoxCollider2D huntSlab;
    private Rigidbody2D huntRb;
    private BeakStrike huntStrike;
    private bool huntStrikeOnSlab;
    private Vector2 lastHuntSize;
    private int huntIncludeMask;
    private int huntExcludeMask;
    private bool huntQueryLayersLatched;
    private Vector3 neckRestLocal;
    private bool neckRestLatched;
    // B-PERCH-27: the body-drop picture's rest transforms (latched once).
    private Vector3 bodyDropLookRest;
    private Vector3 bodyDropTorsoRest;
    private Vector3 bodyDropThighRestL;
    private Vector3 bodyDropThighRestR;
    private Transform bodyDropTorso;
    private bool bodyDropHasLook;
    private bool bodyDropHasTorso;
    private bool bodyDropHasLegs;
    private bool bodyDropRestLatched;
    private float lastBeakZ;
    private float lastTailZ = 999f;
    private OrganismVoice voice;
    private float nextVoiceLookup;
    private int voiceMisses;
    private float nextBlinkAt;
    private float blinkUntil;
    private bool lastEyeOn = true;
    private bool lastNictOn;
    private bool lastLidOn;
    private Vector3 lastEyeLocal = new Vector3(999f, 999f, 999f);
    private bool lastCropOn;
    private float lastCropAmt = float.NaN;
    private bool lastTongueOn;
    private float lastTongueAmt = float.NaN;
    private bool lastWattleOn;
    private float lastWattleAmt = float.NaN;
    private bool lastCombOn;
    private float lastCombAmt = float.NaN;
    private BirdMode poseMode;
    private float settleUntil;
    private float settleBorn;
    private bool wasDirtFly;
    private bool wasPerchedRest;
    private float trailUntil;
    private float trailBorn;
    private float wingUntil;
    private float wingBorn;
    private float tailUntil;
    private float tailBorn;
    private float takeoffEyeUntil;
    private float takeoffEyeBorn;
    private float takeoffLidUntil;
    private float takeoffLidBorn;
    private float takeoffGapeUntil;
    private float takeoffGapeBorn;
    private float takeoffWattleUntil;
    private float takeoffWattleBorn;
    private float takeoffCombUntil;
    private float takeoffCombBorn;
    private float turnUntil;
    private float turnBorn;
    private float walkTurnUntil;
    private float walkTurnBorn;
    private float airTurnUntil;
    private float airTurnBorn;

    // Simulated sit on a BirdPerch trigger. Not a solver contact.
    public bool IsPerched => perchPad != null && !IsFlightMode();

    void Awake()
    {
        sensors = GetComponent<BirdSensors>();
        flight = GetComponentInChildren<BirdFlight>();
        bird = GetComponent<Bird>();
        Transform body = transform.Find("Body");
        bodyRb = body != null ? body.GetComponent<Rigidbody2D>() : null;
        if (bird != null && bird.rig == BirdRig.Flock)
        {
            flockRig = true;
            if (sensors != null) sensors.drivenExternally = true;
            if (flight != null) flight.drivenExternally = true;
        }
        // B-ARC-2: the fall-law control take. -birdLandingRule 0 obeys the brain's
        // ground mode mid-air again (the B-NAT-0 free fall); unset keeps the clamp.
        if (Bird.LandingRuleKnob >= 0f)
            landingRule = Bird.LandingRuleKnob > 0.5f;
        // Initialize runs before this Awake (BuildFlock order), so
        // owner.controller is still null there. Wire `this` once.
        if (sensors != null)
            sensors.BindHuntControl(this);
        if (flight != null)
        {
            BodyAero aero = flight.GetComponent<BodyAero>();
            if (aero != null)
                aero.BindControl(this);
        }
        nextBlinkAt = Time.time + 0.6f + (GetHashCode() & 7) * 0.18f;
        LatchPeckReachRest();
        // AttachHit adds BeakStrike after this Awake; Start / Bind still
        // resolve lazily. Cache here when the stand stamps first.
        huntStrike = GetComponent<BeakStrike>();
        // Fixture enabled from Awake so the first Peck is an offset write.
        // Creating HuntBeak in ApplyPeckBeakReach left bounds at origin.
        EnsureHuntSlab();
        ParkHuntSlab();
    }

    void Start()
    {
        // BuildBird Awake then AttachHit. First Peck Bind used to miss
        // BeakStrike and OverlapCollider ran on the BirdLimb look-beak
        // (Core×Default off), so Rel Hostile never Hurt.
        if (huntStrike == null)
            huntStrike = GetComponent<BeakStrike>();
    }

    void OnDestroy()
    {
        // World-root Default query. Destroy the eat GO, not the torso.
        if (huntSlab != null)
        {
            if (bodyRb == null || huntSlab.gameObject != bodyRb.gameObject)
                Destroy(huntSlab.gameObject);
            huntSlab = null;
            huntRb = null;
        }
    }

    public float FacingSign()
    {
        return facing >= 0f ? 1f : -1f;
    }

    // Brain or spawn sets the side. Picture lives on Look; forces live in Tick / BirdFlight.
    public void SetFacing(float sign)
    {
        float next = sign >= 0f ? 1f : -1f;
        bool flipped = next != FacingSign();
        facing = next;
        // B-TURN-3: an air arc owns the Look while it runs — the mirror follows the
        // side the physics has reached, not a commanded side still in the air.
        bool arcOwns = bird != null && bird.flight != null && bird.flight.ArcOwnsLook();
        if (bird != null && !arcOwns)
            bird.ApplyLookFacing(facing);
        if (flipped)
        {
            NoteSitTurn();
            NoteWalkTurn();
            NoteAirTurn();
        }
    }

    public bool IsFlightMode()
    {
        return mode == BirdMode.Fly || mode == BirdMode.Glide || mode == BirdMode.Attack;
    }

    // Dirt eat / close. HuntBeak and aero/lift gates share this so a
    // leftover pad Sit cannot park the query at ParkY. Woodpecker pad
    // Peck (no preferDirtLand, height ≥ DirtHuntHeight) stays false —
    // the look-beak stays on Head. Do not fold wander Glide near dirt
    // into this gate — landFlap still needs lift (BirdFlight).
    public bool IsHuntSoil()
    {
        if (preferDirtLand)
            return true;
        // Dirt Walk after preferDirtLand flicker (AI HoldDirtAfterHunt
        // can clear soil one Update while mode stays Walk). Without
        // this, walkThrust / HoldSimulatedSupport / OnHuntDirt park and
        // HuntBeak never Rel Hostile. Pad Walk sits above DirtHuntHeight.
        if (mode == BirdMode.Walk && HuntBodyNearDirt())
            return true;
        // Dirt Peck is soil even while HuntBeak is still live after a
        // preferDirtLand flicker. Leftover pad Sit + NearPerch AABB
        // used to set treeSupport and park Rel Hostile. Woodpecker pad
        // Peck never arms HuntBeak — look-beak stays on Head.
        if (mode == BirdMode.Peck && huntBeakSlabLive)
            return true;
        // Brain can clear preferDirtLand one Update before Hurt while
        // perchPad is still set (AI drops hasPerch; body ReleasePerch
        // lagged). IsPerched then made this false, HuntBeak stayed Park
        // Clear, Rel Hostile never reached BeakStrike. Dirt approach
        // height is soil; woodpecker pad drum sits above it.
        if (mode == BirdMode.Peck && HuntBodyNearDirt())
            return true;
        return mode == BirdMode.Peck && !IsPerched;
    }

    // ---- B-LAND-1: the base landing rule (clamp + flare) -------------------
    // One rule in two halves, both in the base, so every brain that writes a
    // mode is covered without a brain edit.
    //
    // (1) The clamp. A brain writes a ground mode (Sit / Stand / Walk) for a
    // body it believes is down. Mid-air that mode leaves the flight set and
    // the flight model switches off — B-NAT-0 read the fall: 9.8 m/s² (free
    // fall), touchdown |vy| 3.5…6.7 m/s, no flare_peak, and the flock's 20 s
    // run landed nothing. While the body is clear of its support the base
    // refuses the mode, keeps the landing mode Glide and counts the refusal.
    //
    // (2) The flare. The Glide the base holds is flown as a landing: the
    // wings spread (the glide pose plus the landing wing flare), the body
    // pitches up to landingFlarePitch and the sink is braked to
    // landingSinkSpeed before the first contact. BirdFlight reads
    // FlyingLanding() for its side of it.
    //
    // A dirt eat is not a landing: while a HuntBeak is armed (an eat is
    // pending) the hunt keeps its own stoop and this rule stands aside.
    [Header("Landing rule (B-LAND-1)")]
    [Tooltip("Base clamps the brain's ground mode while the body is airborne. 0 — obey the brain (the free fall).")]
    public bool landingRule = true;
    [Tooltip("Landing flare: sink the approach brakes to at first contact, m/s (the glide's own cap stands above the flare).")]
    public float landingSinkSpeed = 0.30f;
    [Tooltip("Landing flare: nose-up pitch at first contact, degrees (the cruise glide's 22° read as no flare).")]
    public float landingFlarePitch = 34f;
    [Tooltip("Landing rule: how low above the standing root the clamp holds (m). Its own clearance, not takeoffClearance: the takeoff gate's 0.28 m released the glide 0.43 m over the dirt and the last 0.22 m was a free fall (touchdown |vy| 2.08). At 0.08 the feet report contact first.")]
    public float landingClearance = 0.08f;
    [System.NonSerialized] public int airborneGroundModeRefusals;
    [System.NonSerialized] public int landingGlides;
    private bool landingHeld;

    // The body is in the air and clear of whatever holds it. Sensors are one
    // step stale: the brains run in Update, this is FixedUpdate, and every
    // other mode gate in this step reads the same reading.
    //
    // The clamp's own clearance, not the takeoff gate's: takeoffClearance
    // (0.28 m measured from the standing root = 0.43 m over the dirt) is the
    // height a takeoff has to clear, and the ruler's first post-change run
    // showed the glide released there — the last 0.22 m was a free fall and
    // the touchdown read |vy| 2.08 m/s against a 1.0 m/s bar. landingClearance
    // keeps the glide on until the foot probe reports contact (measured at
    // 0.05 m in this same frame), so the touchdown carries the flare's sink.
    //
    // A pad hold is support: IsPerched keeps the clamp off a bird the perch
    // path has already planted. Without it the ruler's tree run sat on a pad
    // 0.135 m over the dirt, read "airborne" every other step and the clamp
    // yanked the Sit into Glide — grounded flickered and the run filed 4245
    // fake touchdowns.
    public bool AirborneClearOfSupport()
    {
        if (!landingRule || sensors == null || bodyRb == null || bird == null)
            return false;
        if (IsPerched)
            return false;
        if (sensors.bothGrounded || sensors.treeSupport)
            return false;
        return bodyRb.position.y - Bird.StanceRootY(bird) > landingClearance;
    }

    // B-LAND-1: the base owns this descent (the clamp is holding a landing).
    // The pitch, the flare and the sink cap are the landing's, and the hunt's
    // dirt-plate plant must stand aside: OnDirtPlate is true below
    // Insect.DirtHuntHeight (0.45 m) whatever the brain asked, so a wander
    // dirt landing — preferDirtLand reaches here as hunt soil — was planted
    // 0.45 m over the dirt, the horizon snapped and the flare read 0.3° for
    // the whole last 0.5 s of the approach. An armed HuntBeak (an eat is
    // pending) keeps its own path, as with FlyingLanding.
    public bool LandingOwnsDescent()
    {
        if (!landingHeld || huntBeakSlabLive || huntEatLatch)
            return false;
        return AirborneClearOfSupport();
    }

    // B-LAND-1: the base owns this descent. BirdFlight reads it to fly the
    // landing glide — lift on, the flare pitch, the sink cap — where the
    // brain's dirt ask used to be read as hunt soil and switch the whole
    // flight model off. An armed HuntBeak keeps its own path.
    public bool FlyingLanding()
    {
        return mode == BirdMode.Glide && LandingOwnsDescent();
    }

    // B-TAKEOFF-2: the leap. The old takeoff was the wing's alone: the brain
    // wrote Fly, BirdFlight switched the lift on and the bird climbed off its
    // own feet. A crow's takeoff is a jump first — the legs load through a
    // crouch and drive the body up; the wings only take over once it is off
    // the ground. This is that impulse. Read by the ruler: leaps (count),
    // debugLeapVy (the vertical speed the legs gave), debugLeapAt (when); the
    // leap's own share of vy is debugLeapVy over the vy measured 0.15 s later.
    //
    // The edge stands aside while the base flies a landing (the clamp owns
    // that descent) and while an eat is armed (the hunt's plate is its path).
    public bool LeapOwnsSupport()
    {
        if (leapAmp <= 0.01f || bodyRb == null || energyRest)
            return false;
        return leapPhase == 1 || leapPhase == 2;
    }

    private void TickLeap()
    {
        if (leapAmp <= 0.01f || bodyRb == null || sensors == null || energyRest)
            return;
        float now = Time.fixedTime;

        if (leapPhase == 1)
        {
            // The wind-up: the legs fold and the body sinks on its own feet.
            // A velocity write, not a MovePosition — the ruler reads a
            // teleport as a snap.
            if (bodyRb.bodyType == RigidbodyType2D.Dynamic)
            {
                Vector2 v = bodyRb.linearVelocity;
                v.y = -Mathf.Abs(leapCrouchDepth) / Mathf.Max(0.02f, leapCrouchSeconds);
                bodyRb.linearVelocity = v;
            }
            if (now >= leapPhaseUntil)
            {
                leapPhase = 2;
                leapPhaseUntil = now + Mathf.Max(0.02f, leapDriveSeconds);
                if (bodyRb.bodyType == RigidbodyType2D.Dynamic)
                {
                    float vy = Mathf.Max(0f, leapVy * Mathf.Clamp(leapAmp, 0f, 1.5f));
                    bodyRb.AddForce(new Vector2(0f, vy * bodyRb.mass), ForceMode2D.Impulse);
                    debugLeapVy = vy;
                    debugLeapAt = now;
                    leaps++;
                }
                else
                {
                    // Kinematic pad: the hold has the body. Count it and give
                    // the edge back — a refusal, not a silent no-op.
                    leapRefusals++;
                    leapPhase = 0;
                    leapCoolUntil = now + 0.4f;
                }
            }
            return;
        }

        if (leapPhase == 2)
        {
            if (now >= leapPhaseUntil)
            {
                leapPhase = 0;
                leapCoolUntil = now + Mathf.Max(0.2f, leapSettleSeconds) + 0.4f;
            }
            return;
        }

        if (now < leapCoolUntil)
            return;

        bool grounded = sensors.leftFootGrounded || sensors.rightFootGrounded;
        if (!grounded || mode != BirdMode.Fly)
            return;
        if (LandingOwnsDescent() || huntBeakSlabLive || huntEatLatch)
        {
            leapRefusals++;
            return;
        }
        if (bodyRb.position.y - Bird.StanceRootY(bird) > takeoffClearance)
            return;

        leapCrouchBorn = now;
        leapPhase = 1;
        leapPhaseUntil = now + Mathf.Max(0.02f, leapCrouchSeconds);
    }

    private static bool IsGroundMode(BirdMode m)
    {
        return m == BirdMode.Sit || m == BirdMode.Stand || m == BirdMode.Walk;
    }

    private void TickLandingRule()
    {
        if (!AirborneClearOfSupport())
        {
            // Contact. The brain's ground mode is honoured from here.
            landingHeld = false;
            return;
        }
        if (IsGroundMode(mode))
        {
            // The brain is sitting down in the air. Keep the landing mode and
            // count it — the count is the ruler's own reading of how often the
            // brains still ask for the ground mid-air.
            mode = BirdMode.Glide;
            airborneGroundModeRefusals++;
        }
        if (mode == BirdMode.Glide)
        {
            if (!landingHeld)
            {
                landingHeld = true;
                landingGlides++;
            }
            return;
        }
        landingHeld = false;
    }

    // Physics pose vs stance. No OverlapBox / GetContacts (BIRD-12).
    private bool HuntBodyNearDirt()
    {
        if (bodyRb == null || bird == null)
            return false;
        float h = bodyRb.position.y - Bird.StanceRootY(bird);
        return h < Insect.DirtHuntHeight;
    }

    // Hunt dirt: BeakStrike OverlapCollider reads the HuntBeak physics
    // AABB (Rigidbody2D.position on the Default query body). Snap leftover
    // Fly pitch so walkThrust stays ±X on the Insect film.
    public void SnapHuntHorizon()
    {
        AlignHuntBodyHorizon();
    }

    // Crow dirt approach: keep a slow flap. Held-wing Glide is the pad
    // path (NearPerch and high). Weave pulses at cruise height stay a Glide.
    public bool WantsDirtLandingFlap()
    {
        if (mode != BirdMode.Glide)
            return false;
        // Insect hunt already asked for soil. A flare beat here is leftover
        // Glide lift (0.86…0.94 weight) that holds the body off the plate
        // so BeakStrike never Rel Hostile. Gravity takes leftover Glide down.
        // IsHuntSoil (not preferDirtLand alone): dirt Peck after a flicker
        // still refuses the flare so HuntBeak can Rel Hostile.
        if (IsHuntSoil())
            return false;
        if (bird != null && !bird.UsesDirtLandingFlap)
            return false;
        if (bodyRb == null)
            return false;
        float height = bodyRb.position.y - Bird.StanceRootY(bird);
        float flare = flight != null ? Mathf.Max(0.4f, flight.flareHeight) : 1.8f;
        if (height > flare)
            return false;
        if (sensors != null && sensors.nearPerch && height > 1.0f)
            return false;
        return true;
    }

    // B-TAKEOFF-2: the braking strokes (the landing verdict's #2). A crow does
    // not glide onto its feet — on the way down through the last metre and a
    // half it rows the air under itself with 2…3 deep beats, and the flare takes
    // the rest. The window is a descent under brakeFeedHeight; the strokes are
    // slower (brakeStrokeHz) and deeper (brakeStrokeAmp) than the cruise's, and
    // the count is spent, so the wing stops rowing and holds for the touch.
    public bool BrakingStrokesActive()
    {
        debugBrakeOn = false;
        if (bodyRb == null || brakeStrokes <= 0 || brakeStrokeHz <= 0.05f)
            return false;
        if (mode != BirdMode.Glide && mode != BirdMode.Fly)
            return false;
        // B-WING-3: the hunt's own path (an armed slab / a live eat) keeps the
        // wing off the air; a plain landing on hunt dirt is exactly where the
        // chute belongs, so the old soil guard stands aside for the descent.
        if (huntBeakSlabLive || huntEatLatch)
            return false;
        float height = bodyRb.position.y - Bird.StanceRootY(bird);
        // B-WING-3: the descent arms the row itself. Both landing intents above
        // stand aside while the brain's dirt ask reads as hunt soil, so the
        // brake never fired on the wander's own landings (0/6002 frames of
        // B-TAKEOFF-2); a Glide sinking through the feed height IS the landing's
        // last metre.
        bool sinking = bodyRb.linearVelocity.y < -0.15f;
        if (!sinking)
        {
            // Level or climbing: the row's next descent starts with a full
            // count. The wander crow flies low (0.4…0.5 m) and never re-armed
            // above the ceiling, so one chute per run was all the old reset
            // could give.
            brakeStrokesLeft = brakeStrokes;
            return false;
        }
        bool landing = FlyingLanding() || WantsDirtLandingFlap();
        if (!landing && mode != BirdMode.Glide)
            return false;
        if (height > brakeFeedHeight)
            return false;
        if (brakeStrokesLeft <= 0)
            return false;
        debugBrakeOn = true;
        return true;
    }

    // BeakStrike ticks in Update (order 20). LateUpdate pose is too late,
    // and FixedUpdate is before the physics step moves the body — the
    // dirt slab then rides with leftover vx and misses Rel Hostile.
    void Update()
    {
        if (energyRest)
        {
            RestorePeckReach();
            huntBeakSlabLive = false;
            huntEatLatch = false;
            return;
        }
        // Brain writes Peck / preferDirtLand at order 0. BeakStrike is
        // order 20 and OverlapCollider-reads the HuntBeak fixture: a live
        // perchPad made WantsHuntBeakSlab false, so the query stayed at
        // ParkY and Rel Hostile never reached Hurt. Drop the pad volume
        // and the sit here (not only the collider) so this Update arms
        // the dirt strip. IsHuntSoil covers preferDirtLand flicker with
        // leftover pad on the dirt film; woodpecker pad Peck stays off.
        if (IsHuntSoil())
        {
            SetPerchEnabled(false);
            if (perchPad != null)
                ReleasePerch();
        }
        else if (mode == BirdMode.Peck && !IsPerched)
            SetPerchEnabled(false);
        ApplyPeckBeakReach();
    }

    void FixedUpdate()
    {
        if (energyRest)
        {
            RestorePeckReach();
            return;
        }
        if (sensors == null) return;

        // Demo Stand→Fly is leftover cruise. Hunt already asked for soil;
        // a flap here never Rel Hostile (hoverMean holds the body off the
        // Insect). Brain writes Walk/Peck — eat stays the beak.
        if (mode == BirdMode.Stand && takeoffDelay > 0.01f && !preferDirtLand)
        {
            standElapsed += Time.fixedDeltaTime;
            if (standElapsed >= takeoffDelay)
                mode = BirdMode.Fly;
        }

        // Demo about-face. A hunt Peck still followed this and
        // SetFacing yanked walkThrust off the Insect; HuntBeak no
        // longer tracks the nose, but vx along the new face still
        // walks the body (and the slab) off Rel Hostile.
        if (turnAfter > 0.01f && !didAutoTurn && Time.fixedTime >= turnAfter
            && !IsHuntSoil())
        {
            SetFacing(-FacingSign());
            didAutoTurn = true;
        }

        // B-LAND-1: the base landing rule. The brains write Sit / Stand / Walk
        // for a body they believe is down (B-NAT-0 read it: the solo's fly bout
        // expired mid-air and the mode left the flight set, so BirdFlight
        // switched the lift off and the bird fell at 9.8 m/s² with folded
        // wings — touchdown |vy| 3.5…6.7, no flare_peak, landCount 0 in 20 s).
        // While the body is clear of its support the base refuses those modes,
        // keeps the landing mode Glide and counts the refusal. Sits after the
        // Stand->Fly demo, so a scripted takeoff still wins.
        TickLandingRule();

        // B-PERCH-11: THE SETTLE WINDOW (the ring seat's own hold). The drive
        // writes its mode every FRAME; from the seat on it wrote Fly (bPCH10b:
        // mode=2 at every step, the bird lifted 0.06 s after the touch) while
        // the accepted take's steps read Sit (bPCH3d: mode=4 across the hold).
        // While the seat's window is open the step state is Sit: the pad is
        // kept through the flicker, HoldSimulatedSupport welds the body to the
        // hold, and the sensors' grounded/treeSupport restore so the wander
        // syncs its own Sit phase. No hold of its own - the window only writes
        // the state the pad machinery already reads.
        if (perchPad != null && Time.time < perchSettleUntil && IsFlightMode())
        {
            dbgSettleHold++;
            mode = BirdMode.Sit;
        }

        bool flying = IsFlightMode();
        NoteLandSettle();
        if (flockRig)
        {
            // Hunt dirt: drop the pad volume before the foot probe so
            // Tick cannot read a twig as soil (BIRD-13). No GetContacts.
            // SetPerchEnabled(false) alone left perchPad set — HoldSimulated
            // then Kinematic-froze the body on the twig every step, so
            // walkThrust never closed and HuntBeak stayed Park Clear
            // (include Nothing) until a Peck that never came. Ragdoll
            // FixedUpdate already ReleasePerch on preferDirtLand; flock
            // must match. Park Clear / Bake Default / no GetContacts.
            if (IsHuntSoil())
            {
                SetPerchEnabled(false);
                if (perchPad != null)
                    ReleasePerch();
            }
            sensors.Tick();
            NoteTakeoffTrail();
            TickLeap();
            TickFlock(flying);
            // Once, as on the ragdoll path. CacheFlockPose is picture;
            // a hop-turn return used to skip walkThrust and stall the hunt.
            ApplyWalkThrust();
            ApplyGroundGovernor();
            if (flight != null)
                flight.Tick();
            HoldSimulatedSupport();
            // BeakStrike OverlapCollider reads the HuntBeak physics AABB.
            // Peck/Attack only: hunt Walk used to teleport a Dynamic
            // query every 200 Hz step (BIRD-12). Rigidbody2D.position
            // on HuntBeak, not collider.offset at origin and
            // not a Body-child Transform write. No GetContacts.
            if (HuntBeakEatNow() || huntEatLatch)
                PoseLiveHuntSlab();
            TickHuntEatHold();
            SumDebugLift();
            return;
        }

        NoteTakeoffTrail();
        TickLeap();
        DriveWings();
        DriveLegs(flying);
        DriveNeckAndHead();
        DriveTail(flying);
        ApplyWalkThrust();
        // Ragdoll has no TickFlock ReleasePerch. Leftover pad Sit left
        // perchPad set, HuntBeak parked, OverlapCollider never Rel Hostile.
        // Match flock: IsHuntSoil (not preferDirtLand alone) so a dirt
        // Peck after preferDirtLand drop still frees the body.
        if (IsHuntSoil())
        {
            SetPerchEnabled(false);
            if (perchPad != null)
                ReleasePerch();
        }
        // Ragdoll BirdFlight.Tick (order 50) may still snap Y after this
        // step; RefreshHuntBeakPose there rewrites HuntBeak position so
        // Simulate does not keep ParkY / a punch-through delta.
        // Peck/Attack only — hunt Walk is not a Dynamic 200 Hz island.
        // No GetContacts.
        if (HuntBeakEatNow() || huntEatLatch)
            PoseLiveHuntSlab();
        TickHuntEatHold();
        SumDebugLift();
    }

    // Flock: no muscles and no hinges. The flap is a local picture rotation;
    // lift still comes from BirdFlight via debugDownstroke.
    private void TickFlock(bool flying)
    {
        debugDownstroke = false;
        debugChuteOn = false;
        float shoulder = flapMidAngle;
        float elbow = -12f;
        float wrist = 0f;
        // B-WING-4: the fan's target for this step — 1 only where the wing is
        // spread (the burst's downstroke, the held glide pocket, the glide, the
        // chute's downstroke); every other row leaves it folded.
        float feather = 0f;
        // B-WING-3: the parachute. The glide down the last metre used to hold
        // the wing still; the picture now rows it: 2…3 deep slow strokes,
        // spread on the downstroke and folded on the upstroke.
        bool brakePicture = BrakingStrokesActive();
        // Leftover Fly while hunt soil: picture flap still armed
        // debugDownstroke. Lift is already off; do not beat the wing.
        if ((mode == BirdMode.Fly || WantsDirtLandingFlap()) && !IsHuntSoil())
        {
            EvaluateFlap(out shoulder, out elbow, out wrist, out debugDownstroke);
            BlendCruiseGlideWings(ref shoulder, ref elbow, ref debugDownstroke);
            ApplyTakeoffWingStroke(ref shoulder, ref elbow);
            // B-WING-3: the deployment's own pair — the downstroke is the
            // spread (the wing straight, the full reach), the upstroke folds
            // the elbow and the wrist (a shorter wing, less drag).
            if (debugDownstroke)
            {
                elbow = deployGlideElbow;
                wrist = 0f;
            }
            else
            {
                elbow = -deployElbowFold;
                wrist = deployWristFold;
            }
            // B-WING-4: the fan rides the same pair — spread on the downstroke.
            feather = debugDownstroke ? 1f : 0f;
            // The held-wing pocket is a glide: the wing lies out, straight.
            float hold = CruiseHoldBlend();
            if (hold > 0.35f && !debugBrakeOn)
            {
                float g = Mathf.Clamp01(hold);
                elbow = Mathf.Lerp(elbow, deployGlideElbow, g);
                wrist = Mathf.Lerp(wrist, 0f, g);
                shoulder = Mathf.Lerp(shoulder, deployGlideShoulder, g);
                // B-WING-4: the held wing is spread — the fan stays open.
                feather = 1f;
            }
            debugChuteOn = brakePicture;
        }
        else if (mode == BirdMode.Glide && brakePicture)
        {
            // The row under the bird: the same stroke, the chute's deeper cup.
            EvaluateFlap(out shoulder, out elbow, out wrist, out debugDownstroke);
            if (debugDownstroke)
            {
                elbow = deployGlideElbow;
                wrist = 0f;
            }
            else
            {
                elbow = -deployElbowFold;
                wrist = deployChuteWrist;
            }
            // B-WING-4: the parachute's own spread — open on the downstroke.
            feather = debugDownstroke ? 1f : 0f;
            debugChuteOn = true;
        }
        else if (mode == BirdMode.Glide)
        {
            // The held wing: raised out of the body silhouette and straight
            // (spread), not the old bar lying along the back.
            shoulder = deployGlideShoulder;
            elbow = deployGlideElbow;
            // B-WING-4: the held wing is the round's own spread phase.
            feather = 1f;
        }
        else if (mode == BirdMode.Attack)
        {
            // Stoop: no flap. Pin is CacheFlockPose (fold −90 is along the
            // back; dropping shoulder here would read as preen-lift).
            shoulder = flapMidAngle;
            elbow = -42f;
        }
        else if (mode == BirdMode.Sit || mode == BirdMode.Peck)
        {
            // B-WING-6: the folded pack lies along the back (the old -62/0
            // stacked the plates up the forearm — a vertical bundle).
            shoulder = flapMidAngle;
            elbow = groundFoldUlnaZ;
            wrist = groundFoldWristZ;
        }
        else
        {
            // Stand/Walk: the same laid-back fold (was -50/0).
            shoulder = flapMidAngle;
            elbow = groundFoldUlnaZ;
            wrist = groundFoldWristZ;
        }

        ApplyLandingWingFlare(ref shoulder, ref elbow);
        CacheFlockPose(shoulder, elbow, wrist, feather, flying);
        ApplyCryPicture();

        // Flat height is vs dirt (y = −2). A tree slot sits higher, so
        // NearPerch is the approach gate on a branch (BIRD-5).
        // Fly is cruise: a hop under perchApproachHeight is not a landing,
        // or the crow taxis along the soil under the apple AABB.
        // Glide is the land — pad first unless the brain asked for dirt
        // (preferDirtLand). NearPerch AABB is soil plus crown; enabling
        // the perch volume there snapped every dirt Glide onto a twig.
        float height = bodyRb != null ? bodyRb.position.y - Bird.StanceRootY(bird) : 99f;
        bool nearBranch = sensors.nearPerch;
        bool approaching = wasAirborne
            && !IsHuntSoil()
            && sensors.bodyVelocity.y < 0.25f
            && mode == BirdMode.Glide
            && (height < perchApproachHeight || nearBranch);
        // Hunt Walk used to leave the perch volume on (!flying). Pad
        // OnTriggerStay then OfferPerch every stay — GetComponentInParent
        // and a twig Sit, so BeakStrike never Rel Hostile on dirt.
        // No OverlapBox / GetContacts on the foot (BIRD-12, 200 Hz).
        bool huntDirtPose = IsHuntSoil();
        SetPerchEnabled(!huntDirtPose && (!flying || approaching));

        if (flying)
        {
            // B-PERCH-9: the ring trigger offers the pad inside the arrival
            // window; ReleasePerch below cleared offeredPerch in the same
            // step, before TryGlideLand could read it (B-PERCH-8: offers
            // 8830, seats 0). Keep this step's offer for the glide read;
            // the canLand gate still refuses a fast pass.
            BirdPerch glideOffer = offeredPerch;
            bool glideOfferHold = offeredPerchHold;
            if (glideOffer != null)
                dbgOfferSeen++;
            ReleasePerch();
            if (!sensors.bothGrounded && height > takeoffClearance)
                wasAirborne = true;

            // Pad Glide may Sit. Dirt hunt is Walk/Peck: a Sit+Settle
            // snap here stole Rel Hostile from the beak.
            if (mode == BirdMode.Glide && !IsHuntSoil())
                TryGlideLand(nearBranch, glideOffer, glideOfferHold);
        }
        else if (huntDirtPose)
        {
            if (offeredPerch != null)
                dbgHuntClear++;
            // Hunt Walk/Peck is soil. Do not OverlapBox pads at 200 Hz,
            // and do not stay kinematic on a twig — BeakStrike never
            // reaches Rel Hostile from a pad Sit.
            offeredPerch = null;
            if (perchPad != null)
                ReleasePerch();
        }
        else
        {
            if (offeredPerch != null)
                dbgSitOffer++;
            // Held pad first: a sit must not OverlapBox+GetComponent every 200 Hz.
            // Fly is the release. Do not steal a far pad: a 4 m search snapped
            // the whole flock onto one twig, and dirt Sit under the crown jumped.
            BirdPerch pad = perchPad ?? offeredPerch ?? TryFindPerchPad();
            if (pad == null && mode == BirdMode.Sit && nearBranch && height > 0.45f)
                pad = TryFindNearestPad(0.55f);
            if (pad != null)
            {
                dbgSitAttach++;
                AttachToPerch(pad);
            }
        }
    }

    // Glide sits on a pad if the perch volume overlaps one. Dirt Sit is
    // allowed even under the tree AABB — that AABB is soil plus crown.
    // Hunt soil skips the pad: otherwise a dirt Glide through the
    // crown Sits on a twig and never reaches soil. Fly still does not Sit.
    private void TryGlideLand(bool nearBranch, BirdPerch offer = null, bool holdOffer = false)
    {
        _ = nearBranch;
        // B-PERCH-9: the offer read BEFORE the flying branch's ReleasePerch
        // (the caller passes its own step's offer), so a trigger offer made
        // in the arrival window binds here in Glide. The hold offer is the
        // seat pose itself, so it does not need the air latch; the slow gates
        // still refuse a fast pass.
        BirdPerch pad = IsHuntSoil() ? null : (offer ?? offeredPerch ?? TryFindPerchPad());
        bool canLand = (wasAirborne || holdOffer)
            && sensors.bodyVelocity.y < 0.5f
            && Mathf.Abs(sensors.bodyVelocity.x) < 3.5f;
        if (!canLand)
        {
            if (pad != null)
                dbgLandRefused++;
            return;
        }

        if (pad != null)
        {
            dbgLandBound++;
            mode = BirdMode.Sit;
            takeoffDelay = 0f;
            wasAirborne = false;
            AttachToPerch(pad);
            return;
        }

        // Dirt: Sit only after the glide has already bled speed. Instant
        // Sit+impulse while still falling is a snap into the ground.
        if (sensors.bothGrounded)
        {
            bool slow = sensors.bodyVelocity.y > -0.55f
                && Mathf.Abs(sensors.bodyVelocity.x) < 1.6f;
            if (!slow)
                return;
            mode = BirdMode.Sit;
            takeoffDelay = 0f;
            wasAirborne = false;
            SettleLanding();
        }
    }

    // B-PERCH-9: the ring trigger's offer from its own arrival window (the
    // hold pose). Distinct from the pad's trigger: the hold offer was made at
    // the seat pose, with the bird slow and at the slot, so the glide attach
    // may take it even when the air latch never rose (bPCH9b: the crown
    // arrival kept the feet reading support, wasAirborne stayed false, and
    // all 8830 offers were dropped by the air gate).
    public void OfferPerchHold(BirdPerch pad)
    {
        if (!flockRig || pad == null)
            return;
        if (IsHuntSoil())
            return;
        offeredPerch = pad;
        offeredPerchHold = true;
    }

    // Pad trigger overlapped the flock perch volume. Attach only in TickFlock.
    public void OfferPerch(BirdPerch pad)
    {
        if (!flockRig || pad == null)
            return;
        // IsHuntSoil: preferDirtLand flicker + dirt Peck must not queue
        // a twig Sit that parks HuntBeak (Rel Hostile miss). No GetContacts.
        if (IsHuntSoil())
            return;
        offeredPerch = pad;
    }

    private BirdPerch TryFindPerchPad()
    {
        if (bird == null || bird.perchCollider == null || !bird.perchCollider.enabled)
            return null;
        Bounds b = bird.perchCollider.bounds;
        int n = Physics2D.OverlapBoxNonAlloc(b.center, b.size, 0f, perchHits, GroundLayers.Mask);
        BirdPerch best = null;
        float bestD = float.MaxValue;
        Vector2 p = bodyRb != null ? bodyRb.position : (Vector2)transform.position;
        for (int i = 0; i < n; i++)
        {
            Collider2D hit = perchHits[i];
            BirdPerch pad = BirdPerch.FromCollider(hit);
            if (pad == null)
                continue;
            float d = ((Vector2)pad.transform.position - p).sqrMagnitude;
            if (d < bestD)
            {
                bestD = d;
                best = pad;
            }
        }
        return best;
    }

    // Sit under the crown with no pad in the perch volume (spawn still on
    // dirt, or SeatBirds moved the root before the body RB caught up).
    private BirdPerch TryFindNearestPad(float range)
    {
        if (bodyRb == null)
            return null;
        float size = Mathf.Max(0.6f, range);
        int n = Physics2D.OverlapBoxNonAlloc(
            bodyRb.position, new Vector2(size, size), 0f, perchHits, GroundLayers.Mask);
        BirdPerch best = null;
        float bestD = float.MaxValue;
        Vector2 p = bodyRb.position;
        for (int i = 0; i < n; i++)
        {
            BirdPerch pad = BirdPerch.FromCollider(perchHits[i]);
            if (pad == null)
                continue;
            float d = ((Vector2)pad.transform.position - p).sqrMagnitude;
            if (d < bestD)
            {
                bestD = d;
                best = pad;
            }
        }
        return best;
    }

    private void AttachToPerch(BirdPerch pad)
    {
        if (pad == null || bodyRb == null)
            return;
        offeredPerch = pad;
        if (perchPad != pad)
        {
            if (perchPad != null)
                perchPad.ClearOccupant(this);
            perchPad = pad;
            pad.BindOccupant(this);
            BindPerchLocal();
        }
        wasAirborne = false;
    }

    private void BindPerchLocal()
    {
        if (perchPad == null || bodyRb == null)
            return;
        Vector2 hold = perchPad.HoldBodyPosition(bird, bodyRb.position.x);
        Vector2 padW = perchPad.CurrentPadWorld();
        perchHoldPos = hold;
        perchHoldLocal = Quaternion.Inverse(perchPad.CurrentLookRotation())
            * (Vector3)(hold - padW);
    }

    private Vector2 PerchWorldHold()
    {
        if (perchPad == null)
            return perchHoldPos;
        Vector2 padW = perchPad.CurrentPadWorld();
        perchHoldPos = padW + (Vector2)(perchPad.CurrentLookRotation() * perchHoldLocal);
        return perchHoldPos;
    }

    // After look-sway: ride the branch picture, not last physics pose.
    public void FollowPerchVisual()
    {
        if (energyRest || perchPad == null || bodyRb == null || IsFlightMode())
            return;
        Vector2 hold = PerchWorldHold();
        bodyRb.position = hold;
        bodyRb.rotation = 0f;
        bodyRb.linearVelocity = Vector2.zero;
        bodyRb.angularVelocity = 0f;
    }

    private void ReleasePerch()
    {
        if (perchPad != null)
            perchPad.ClearOccupant(this);
        perchPad = null;
        offeredPerch = null;
        offeredPerchHold = false;
        if (bodyRb != null && !energyRest && bodyRb.bodyType == RigidbodyType2D.Kinematic)
            bodyRb.bodyType = RigidbodyType2D.Dynamic;
    }

    // Tree sit is kinematic on the pad. Dirt sit/walk cancels weight so the
    // trigger feet do not sink — not a flap, a simulated contact normal.
    private void HoldSimulatedSupport()
    {
        if (!flockRig || bodyRb == null || energyRest)
            return;

        // Dirt hunt (preferDirtLand / dirt Peck): never weld to a leftover
        // pad. perchPad after SetPerchEnabled(false) used to Kinematic-freeze
        // walkThrust here while HuntBeak stayed Park Clear — Rel Hostile
        // never reached Peck Bake. Woodpecker pad Peck is not IsHuntSoil.
        if (perchPad != null && IsHuntSoil())
            ReleasePerch();

        if (perchPad != null && !IsFlightMode())
        {
            if (bodyRb.bodyType != RigidbodyType2D.Kinematic)
                bodyRb.bodyType = RigidbodyType2D.Kinematic;
            bodyRb.linearVelocity = Vector2.zero;
            bodyRb.angularVelocity = 0f;
            bodyRb.MovePosition(PerchWorldHold());
            bodyRb.MoveRotation(0f);
            return;
        }

        if (bodyRb.bodyType == RigidbodyType2D.Kinematic)
            bodyRb.bodyType = RigidbodyType2D.Dynamic;

        // Hunt Walk is written on OnDirtPlate (height < 0.45) before the
        // foot probe. That 0.45 m is the brain's approach, not the soil:
        // cancelling vy there hovered a leg above the Insect, and the
        // beak slab never Rel Hostile. Hold weight only on the plate.
        // IsHuntSoil (not preferDirtLand alone): AI can clear preferDirtLand
        // one Update before Hurt while dirt Peck + HuntBodyNearDirt still
        // needs the plant / horizon so HuntBeak Rel Hostile. Woodpecker
        // pad Peck is not IsHuntSoil.
        bool dirtHold = sensors != null && sensors.bothGrounded;
        // B-LAND-1: not while the base flies a landing. Below 0.45 m the plate
        // read planted the body, snapped the horizon (pitch 0) and killed the
        // flare — the ruler's last 0.5 s of every p2 approach read 0.3°.
        bool flyingLanding = LandingOwnsDescent();
        if (!dirtHold && !flyingLanding && IsHuntSoil() && OnHuntDirt())
            dirtHold = true;
        // Flock feet are triggers: leftover Fly with huntSoil has no
        // lift and used to skip this hold, so the body fell through the
        // slab and BeakStrike never Rel Hostile. Plant leftover flight
        // on the plate too. Air hunt (height > 0.45) still falls.
        if (dirtHold && !flyingLanding && !LeapOwnsSupport() && (!IsFlightMode() || IsHuntSoil()))
        {
            Vector2 v = bodyRb.linearVelocity;
            float stanceH = bodyRb.position.y - Bird.StanceRootY(bird);
            // Leftover Fly punched through (flock feet are triggers).
            // Frozen under the slab, HuntBeak still sits on dirt Y but
            // walkThrust coasts below the Insect. Put the body back.
            // Leaving leftover Fly at +4 cm (vy kill without a snap)
            // hovered bothGrounded on the 6 cm probe and skipped
            // walkThrust until the brain wrote Walk — Rel Hostile never
            // reached the beak. Snap only after a punch-through: writing
            // Rigidbody2D.position every plate step is a teleport, and
            // that freeze-frame ate walkThrust (comX stuck, Peck never
            // covered the Insect).
            if (IsHuntSoil() && stanceH < 0f)
            {
                Vector2 p = bodyRb.position;
                p.y = Bird.StanceRootY(bird);
                bodyRb.position = p;
                stanceH = 0f;
                v.y = 0f;
            }
            bool onPlate = stanceH <= 0.04f;
            // B-PERCH-25: settle onto the legs. Freezing the leftover vy kept a body
            // that had stopped flying at whatever height it had inside the window
            // above (bPCH24c: bodyY -1.8190 against the -1.8530 stance root, the
            // drawn feet 9 px — 0.02 m — over the drawn ground). While the body
            // rides above the root, draw it down at (gain x stanceH) m/s; at the
            // root the old freeze holds it, so nothing sinks.
            float settleGain = Bird.GroundSettleKnob >= 0f ? Bird.GroundSettleKnob : groundSettleGain;
            if (onPlate && stanceH > 0f && settleGain > 0f)
                v.y = Mathf.Min(v.y, -Mathf.Min(stanceH, 0.04f) * settleGain);
            else if (v.y < 0f && onPlate)
                v.y = 0f;
            // Flock feet are triggers: no solver friction. Leftover Fly
            // cruise (several m/s) coasts through PeckHold and the beak
            // slab leaves the Insect. Cap Walk on the soil film only —
            // DirtHuntHeight 0.45 m is the close; an air cap there
            // killed a 4 m/s Walk. Brain Pecks in InPeckWindow; halt
            // vx on the whole Peck so HuntBeak stays on Rel Hostile.
            // Hurt is BeakStrike.
            if (mode == BirdMode.Peck && IsHuntSoil())
            {
                v.x = 0f;
            }
            else
            {
                bool onFilm = stanceH < HuntSoilFilm && stanceH > -HuntSoilFilm;
                if (IsHuntSoil() && onFilm && walkSpeed > 0.01f)
                {
                    float cap = walkSpeed;
                    if (v.x > cap) v.x = cap;
                    else if (v.x < -cap) v.x = -cap;
                }
            }
            bodyRb.linearVelocity = v;
            // Hunt only. MoveRotation waits for the solver; Update then
            // still saw leftover Fly pitch and BeakStrike OverlapBox
            // (AABB, eulerZ) filled 8 hits with no Insect. Snap physics
            // and Transform so the next Update slab is horizon.
            // walkThrust stays ±X. Not a flap. Wander Sit keeps the pitch spring.
            if (IsHuntSoil())
                AlignHuntBodyHorizon();
            if (onPlate)
            {
                // B-GROUND-1: a planted claw carries the bird's weight when the
                // grip asks for it (-birdClawShare). With a share the pad's
                // contact has a real normal load, so the raised friction under
                // it is real friction and not a number on a material.
                float carry = ClawCarryShare();
                bodyRb.AddForce(
                    new Vector2(0f, bodyRb.mass * -Physics2D.gravity.y * (1f - carry)),
                    ForceMode2D.Force);
            }
        }
    }

    // Bleed leftover impact. Do not zero the step — that snaps into the dirt.
    private void SettleLanding()
    {
        if (bodyRb == null) return;
        float mass = bodyRb.mass;
        Vector2 v = bodyRb.linearVelocity;
        float killX = -v.x * mass * 0.35f;
        float killY = v.y < 0f ? -v.y * mass * 0.55f : 0f;
        bodyRb.AddForce(new Vector2(killX, killY), ForceMode2D.Impulse);
        float wRad = bodyRb.angularVelocity * Mathf.Deg2Rad;
        bodyRb.AddTorque(-wRad * bodyRb.inertia * 0.6f, ForceMode2D.Impulse);
    }

    // Angles from the physics step; localRotation in LateUpdate: the picture is not 200 Hz.
    private void CacheFlockPose(float shoulder, float elbow, bool flying)
    {
        CacheFlockPose(shoulder, elbow, 0f, 0f, flying);
    }

    // B-WING-3: the pose now carries the wrist of the picture. 0 = the outer
    // wing lies in line with the ulna (the old glued plank).
    // B-WING-4: and the fan's own target — 1 in the spread phases.
    private void CacheFlockPose(float shoulder, float elbow, float wrist, float feather, bool flying)
    {
        flockWingFold = -90f + (shoulder - flapMidAngle);
        bool beatUlna = (mode == BirdMode.Fly || WantsDirtLandingFlap()) && !debugDownstroke;
        if (beatUlna && flight != null && CruiseHoldBlend() > 0.35f)
            beatUlna = false;
        flockUlnaZ = beatUlna ? -deployElbowFold : elbow;
        flockLeftWingFold = flockRightWingFold = flockWingFold;
        flockLeftUlnaZ = flockRightUlnaZ = flockUlnaZ;
        // The fold's own pair: on the upstroke the hand rounds the wing down to
        // the elbow's fold, so the tip reach shortens (the deployment); on the
        // downstroke it lies out (the spread).
        flockWristZ = wrist;
        flockLeftWristZ = flockRightWristZ = flockWristZ;
        // B-WING-4: the fan's target, cached with the rest of the picture.
        flockFeatherTarget = feather;
        ApplyStoopWingPicture();

        if (mode == BirdMode.Peck && (sensors.bothGrounded || OnHuntDirt()))
        {
            flockLeftThighZ = -sitHipAngle * 0.35f;
            flockRightThighZ = -sitHipAngle * 0.35f;
            flockLeftShankZ = flockRightShankZ = -12f;
            flockLeftFootZ = flockRightFootZ = 0f;
            ApplySitGraspPicture();
            ApplySitCrouchPicture();
            ApplySitHunchPicture();
            ApplyPeckLungePicture();
            ApplyPeckHeadPicture();
            ApplyPeckWingPicture();
            return;
        }

        flockNeckZ = 0f;
        flockHeadZ = 0f;
        if (mode == BirdMode.Walk && (sensors.bothGrounded || OnHuntDirt()))
        {
            if (ApplyWalkTurnPicture())
            {
                // Brain already wrote SetFacing. Instant Look flip
                // kept the stride, so the about-face read as a swap.
                // walkThrust is FixedUpdate (flock + ragdoll), not here.
                return;
            }
            // Brain already wrote Walk (hen Flee). Neck/wings glance;
            // forage stride/hop still commuted, so Flee Walk read as
            // a dirt scan with hitching wings. Faster gait through
            // the glance. Hop-turn keeps the about-face.
            if (ApplyAlarmLookLegPicture())
            {
                return;
            }
            // Crow dirt Search: crouch and scan the soil before Peck.
            // Stride here read as a commute; hens keep walkHop.
            if (ApplySearchCrouchPicture())
            {
                return;
            }
            // Brain already wrote Walk (ChickenWander). Crow stride on a
            // hen is a sliding pair of sticks; both legs coil together.
            if (ApplyWalkHopPicture())
            {
                return;
            }
            float w = (Mathf.PI * 2f) * WalkGaitPhase();
            float sL = Mathf.Sin(w);
            float sR = Mathf.Sin(w + Mathf.PI);
            flockLeftThighZ = -walkHipAmp * sL;
            flockRightThighZ = -walkHipAmp * sR;
            // Same sin as ragdoll DriveLegs: plus is the forward thigh.
            // Flock hip visual is already minus. Fold that shank so the
            // swing foot clears — Max(0, −sin) bent the trailing stick.
            float swingL = WalkSwing01(sL);
            float swingR = WalkSwing01(sR);
            flockLeftShankZ = -walkKneeAmp * swingL;
            flockRightShankZ = -walkKneeAmp * swingR;
            // Knee folded the shank; the foot stayed a frozen slab on it.
            // Stance cancels parent so the box sits on the dirt; swing
            // drops the toe. Picture only — Walk thrust is still the body.
            if (walkFootAmp > 0.01f)
            {
                flockLeftFootZ = -(flockLeftThighZ + flockLeftShankZ)
                    - walkFootAmp * swingL;
                flockRightFootZ = -(flockRightThighZ + flockRightShankZ)
                    - walkFootAmp * swingR;
            }
            else
                flockLeftFootZ = flockRightFootZ = 0f;
            // One nod per stride of the near leg. Walk was legs-only; the
            // head sat still so forage Walk read as a sliding sit.
            // Brain already wrote Walk (BIRD-11). Legs, hitch, and the
            // nod were live; the bob sat on 0 so dirt forage read as
            // a commute. Dip toward the soil between pecks.
            float bob = walkHeadBob * Mathf.Sin(w);
            float scan = WalkScanDip();
            flockNeckZ = bob - scan;
            flockHeadZ = flockNeckZ * 0.45f;
            // Brain already wrote Walk (BIRD-11). Legs and nod were
            // live; the Sit-adjacent fold made dirt forage read as
            // a sliding sit. Opposite wing hitch on the swing.
            ApplyWalkStrideWingPicture();
            return;
        }

        if (mode == BirdMode.Sit && sensors.bothGrounded)
        {
            flockLeftThighZ = -sitHipAngle;
            flockRightThighZ = -sitHipAngle;
            flockLeftShankZ = flockRightShankZ = -12f;
            flockLeftFootZ = flockRightFootZ = 0f;
            ApplySitGraspPicture();
            ApplySitCrouchPicture();
            ApplySitHunchPicture();
            float roost = NightRoost();
            if (idleHop > 0.01f && roost < 0.35f && sitScratchAmp < 0.01f
                && AlarmLookSit01() < 0.01f)
            {
                float bob = 11f * idleHop;
                flockLeftThighZ -= bob;
                flockRightThighZ -= bob * 0.65f;
                flockLeftShankZ -= 8f * idleHop;
                flockRightShankZ -= 5f * idleHop;
            }

            if (roost > 0.35f)
            {
                // Clock already lengthens Sit at night; without this the
                // bird still looks/preens. Beak under the scapulars, still.
                flockNeckZ = 28f * roost;
                flockHeadZ = 18f * roost;
                ApplyPreenWingPicture(0.85f * roost);
            }
            else if (ApplyLandSettlePicture())
            {
                // Just planted. Idle look waits until the ruffle dies.
            }
            else if (ApplySitTurnPicture())
            {
                // Brain already wrote SetFacing. Instant Look flip
                // read as a texture swap; this is the hop-turn.
            }
            else if (AlarmLookSit01() > 0.01f)
            {
                // Brain already posted Alarm; Sit still forage-look /
                // preen / scratch until Walk/Fly. Freeze the fidget;
                // glance owns neck / tail / eye. Sit startle hitch
                // and hunch are ApplyCryPicture (hunch after hitch).
                // Sit startle crouch and grasp are below. Sit
                // startle tail is ApplyTailFanPicture. Hop-turn
                // keeps the about-face. Walk scramble stays Walk.
                ApplyAlarmLookSitLegPicture();
                ApplyAlarmLookSitGraspPicture();
            }
            else if (ApplySitScratchPicture())
            {
                // Brain already wrote idleHop. Both-leg bob read as a
                // crouch twitch; this is the forage scratch.
            }
            else if (idlePreen > 0.01f)
            {
                flockNeckZ = 24f * idlePreen;
                flockHeadZ = 16f * idlePreen;
                // Neck already turns back; without the wing this reads as look.
                ApplyPreenWingPicture(idlePreen);
                ApplySitPreenWingPicture();
            }
            else if (Mathf.Abs(idleLook) > 0.01f)
            {
                // Look from the pad hunch, not from a dirt-sit neck.
                flockNeckZ += 18f * idleLook;
                flockHeadZ += 11f * idleLook;
                ApplySitLookWingPicture();
            }

            return;
        }

        // Brain already wrote Fly (wander hop / Sit→Fly). Stand+flap on
        // dirt was a helicopter; this loads the spring, then air tucks.
        // Leftover Fly while IsHuntSoil is a dirt hunt: a takeoff
        // crouch here held the body like a hop and BeakStrike never
        // Rel Hostile. IsHuntSoil covers dirt Walk after flicker.
        if (mode == BirdMode.Fly && !IsHuntSoil() && OnDirtSupport()
            && ApplyTakeoffCrouchPicture())
            return;

        bool tuck = flying && !IsHuntSoil() && !sensors.bothGrounded;
        // First air used to snap the belly-tuck, so the hop vanished.
        // Hang, then fold — Attack/Glide keep their own feet.
        if (tuck && ApplyTakeoffTrailPicture())
            return;
        // Brain already wrote Glide. Dirt used to hang a stick; a pad
        // kept the Fly belly-tuck until Sit snapped. Drop the look-legs.
        if (tuck && ApplyLandingGearPicture())
            return;
        if (tuck && mode == BirdMode.Glide && bodyRb != null && flight != null)
        {
            // landGearAmp 0: dirt still untucks to hang, perch stays tucked.
            float h = bodyRb.position.y - Bird.StanceRootY(bird);
            if (!sensors.nearPerch && h < Mathf.Max(0.4f, flight.flareHeight))
                tuck = false;
        }
        // Brain already wrote Attack (play dive / prey stoop). Fly tuck
        // hid the feet against the belly, so the dive read as another flap.
        if (tuck && mode == BirdMode.Attack && ApplyStoopTalonPicture())
            return;
        float z = tuck ? -tuckHipAngle : 0f;
        flockLeftThighZ = z;
        flockRightThighZ = z;
        float knee = tuck ? -tuckKneeAngle : 0f;
        flockLeftShankZ = knee;
        flockRightShankZ = knee;
        flockLeftFootZ = flockRightFootZ = 0f;
        // Brain already wrote Fly/Glide (dirt approach). Wing, tail,
        // pupil, lid, and bill already read the swoop; the belly-tuck
        // held until flare, so the dive still flashed a cruise fold.
        // Overwrite tuck. Air turn still banks. Flare owns landGear.
        ApplySwoopTalonPicture();
        // Brain already wrote SetFacing. Instant Look flip plus
        // bankLook still read as a mirrored cruise; this banks.
        ApplyAirTurnPicture();
    }

    void LateUpdate()
    {
        if (energyRest)
        {
            // Park HuntBeak (unsim). Picture Apply* here still ran on
            // leftover Sit (World flock rest) at display rate — not a
            // force, but 8 sprite writes per resting bird. Hunt is off.
            RestorePeckReach();
            return;
        }
        ApplySitBlink(false);
        ApplyStoopNict();
        ApplyWalkScanEye();
        ApplyWalkScanLid();
        ApplyPeckCrop();
        ApplyCryTongue();
        ApplyCryWattle();
        ApplyCryComb();
        if (!flockRig || bird == null) return;

        if (bird.leftWingRoot != null)
            bird.leftWingRoot.localRotation = Quaternion.Euler(0f, 0f, flockLeftWingFold);
        if (bird.rightWingRoot != null)
            bird.rightWingRoot.localRotation = Quaternion.Euler(0f, 0f, flockRightWingFold);
        if (bird.leftUlna != null)
            bird.leftUlna.localRotation = Quaternion.Euler(0f, 0f, flockLeftUlnaZ);
        if (bird.rightUlna != null)
            bird.rightUlna.localRotation = Quaternion.Euler(0f, 0f, flockRightUlnaZ);
        // B-WING-3: the outer wing. The hand used to keep its build rotation —
        // one more segment of the same plank; the deployment rounds it.
        if (bird.leftHand != null)
            bird.leftHand.localRotation = Quaternion.Euler(0f, 0f, flockLeftWristZ);
        if (bird.rightHand != null)
            bird.rightHand.localRotation = Quaternion.Euler(0f, 0f, flockRightWristZ);
        // B-WING-4: the fan itself. Picture only — the target rides the pose
        // cache, a 70 ms move turns a 6 Hz stroke into a snap instead of a
        // stutter. No physics reads this.
        flockFeatherOpen = Mathf.MoveTowards(flockFeatherOpen, flockFeatherTarget, Time.deltaTime / 0.07f);
        if (bird.leftFan != null)
            bird.leftFan.SetOpen(flockFeatherOpen);
        if (bird.rightFan != null)
            bird.rightFan.SetOpen(flockFeatherOpen);
        SetThigh(bird.leftThigh, flockLeftThighZ);
        SetThigh(bird.rightThigh, flockRightThighZ);
        SetShank(bird.leftShank, flockLeftShankZ, ref lastLeftShankZ);
        SetShank(bird.rightShank, flockRightShankZ, ref lastRightShankZ);
        SetShank(bird.leftFoot, flockLeftFootZ, ref lastLeftFootZ);
        SetShank(bird.rightFoot, flockRightFootZ, ref lastRightFootZ);
        if (bird.neckVisual != null)
            bird.neckVisual.localRotation = Quaternion.Euler(0f, 0f, flockNeckZ);
        if (bird.headVisual != null)
            bird.headVisual.localRotation = Quaternion.Euler(0f, 0f, flockHeadZ);
        if (bird.beakVisual != null && flockBeakZ != lastBeakZ)
        {
            bird.beakVisual.localRotation = Quaternion.Euler(0f, 0f, flockBeakZ);
            lastBeakZ = flockBeakZ;
        }
        ApplyTailFanPicture();
        if (bird.lookRoot != null)
        {
            float roll = playRoll;
            if (flight != null)
                roll += flight.bankLook;
            if (roll != lastLookRoll)
            {
                bird.lookRoot.localRotation = Quaternion.Euler(0f, 0f, roll * 32f);
                lastLookRoll = roll;
            }
        }
        ApplyPeckNeckReachPicture();
        ApplyPeckBodyDropPicture();
        // B-VIS-WALK: the drawn step owns the legs last, on the ground only, so
        // no held branch (crouch, glance, hop-turn) can leave a walking bird
        // frozen mid-pose - the slide the owner read. Picture only.
        if (flockRig && StepPictureActive())
            ApplyStepPicture();
        else
        {
            debugStepOn = 0;
            debugStepPhase = -1f;
        }
        // B-GROUND-1: the claws. Last, so the pad reads the picture this frame
        // actually put on the bones.
        if (flockRig)
            ClawGripLate();
        // Eat slab is Update (before BeakStrike). Rebuilding it after
        // the neck dip inflates the AABB and the 8-hit query misses
        // the Insect.
    }

    private static void SetThigh(Transform thigh, float zDeg)
    {
        if (thigh != null)
            thigh.localRotation = Quaternion.Euler(0f, 0f, zDeg);
    }

    private static void SetShank(Transform shank, float zDeg, ref float lastZ)
    {
        if (shank == null || zDeg == lastZ)
            return;
        shank.localRotation = Quaternion.Euler(0f, 0f, zDeg);
        lastZ = zDeg;
    }

    // Flock tail was a dead box. Fan is the landing read of swoop→flare:
    // positive Z drops the free end (airbrake), negative streams it on the dive.
    // Picture only — DriveTail still holds ragdoll pitch. No Find.
    private void ApplyTailFanPicture()
    {
        if (bird == null || bird.tailVisual == null)
            return;
        float z = 0f;
        if (mode == BirdMode.Peck && IsWoodpeckerDrum())
            z = 22f;
        else if (mode == BirdMode.Sit || mode == BirdMode.Peck || mode == BirdMode.Walk)
        {
            z = 6f;
            // Brain already wrote Sit after Fly/Glide/Attack. Wings,
            // neck, pupil, lid, and bill already ruffle; the fan still
            // rode landSettleAmp (the old slow 18° envelope), so the
            // plant sat on a Sit brake. Independently gated. Hitch
            // with the same 22 Hz rake as the wings. Look / preen /
            // scratch / hop-turn keep their own fans. Pad crouch /
            // Alarm keep theirs. Night roost skip (LandSettle01).
            float settleTail = LandSettleTail01();
            if (settleTail > 0.01f)
            {
                float shake = Mathf.Max(0f, LandSettleShake());
                float hitch = settleTail * (0.70f + 0.30f * shake);
                z = Mathf.Max(z, 6f + 14f * hitch);
            }
            float crouch = SitCrouch01();
            if (crouch > 0.01f)
                z = Mathf.Max(z, 14f * crouch);
            // Brain already wrote idleLook. Neck and pupil weave;
            // the Sit drop left a statue perch, so look-around still
            // read as a nodding head. After crouch: pad Sit would
            // Max the stream back to 14°. Assign, not Max — the
            // stream side is below the Sit 6°. Hop-turn owns
            // sitTurnTailAmp. Scratch owns sitScratchTailAmp
            // after preen.
            float lookTail = SitLookTail01();
            if (Mathf.Abs(lookTail) > 0.01f)
                z = 6f - 12f * lookTail;
            // Brain already wrote idlePreen. Wing, bill, and pupil
            // were live; the Sit drop left a statue perch, so
            // grooming still read as a stretching sit. Drop with
            // the nibble. Look owns sitLookTailAmp. Scratch owns
            // sitScratchTailAmp. Hop-turn owns sitTurnTailAmp.
            // Max, not assign: pad crouch 14° stays when the nibble
            // is small.
            float preenTail = SitPreenTail01();
            if (preenTail > 0.01f)
                z = Mathf.Max(z, 6f + 14f * preenTail);
            // Brain already wrote idleHop. Foot, lid, pupil, wing,
            // and bill were live; the Sit drop (and the old slow
            // 14° envelope on sitScratchAmp) left a statue perch,
            // so scratch still read as a kicking sit. Hitch with
            // the rake. Look / preen keep their own fans. Hop-turn
            // owns sitTurnTailAmp. Max, not assign: pad crouch
            // 14° stays when the kick is small.
            float scratchTail = SitScratchTail01();
            if (scratchTail > 0.01f)
            {
                float kick = Mathf.Max(0f, Mathf.Sin(Time.time * 22f));
                float hitch = scratchTail * (0.70f + 0.30f * kick);
                z = Mathf.Max(z, 6f + 14f * hitch);
            }
            float lunge = PeckLunge01();
            if (lunge > 0.01f)
                z = Mathf.Max(z, 16f * lunge);
            // Brain already wrote Peck (BIRD-8). Gape, swallow, lunge,
            // and the wing brace were live; the Sit drop made forage
            // eat read as a nodding sit. Fan on the jab.
            float peckTail = PeckTail01();
            if (peckTail > 0.01f)
                z = Mathf.Max(z, 6f + 16f * peckTail);
            // Brain already wrote SetFacing. Legs, neck, wings,
            // pupil, lid, and bill hop; the fan still rode
            // sitTurnAmp, so the about-face sat on the hop
            // envelope. Independently gated. Look / preen /
            // scratch keep their own fans. Walk hop-turn owns
            // walkTurnTailAmp. Air turn owns airTurnTailAmp.
            // Max, not assign: pad crouch 14° stays when the
            // plant is small.
            float turnTail = SitTurnTail01();
            if (turnTail > 0.01f)
                z = Mathf.Max(z, 16f * turnTail);
            // Brain already wrote SetFacing. Legs, neck, wings,
            // pupil, lid, and bill hop; the fan still rode
            // walkTurnAmp, so the dirt about-face sat on the hop
            // envelope. Independently gated. Sit hop-turn owns
            // sitTurnTailAmp. Air turn owns airTurnTailAmp.
            // Stride / hen hop keep their own fans. Max, not
            // assign: pad crouch 14° stays when the plant is
            // small.
            float wturnTail = WalkTurnTail01();
            if (wturnTail > 0.01f)
                z = Mathf.Max(z, 16f * wturnTail);
            // Brain already wrote Walk (BIRD-11). Legs, nod, and the
            // opposite-wing hitch were live; the Sit drop made dirt
            // forage read as a sliding perch. Pump twice per cycle.
            float strideTail = WalkStrideTail01();
            if (strideTail > 0.01f)
                z = Mathf.Max(z, 6f + 12f * strideTail);
            // Brain already wrote Walk (ChickenWander). Legs, nod, and
            // the wing shrug were live; the Sit drop made the bounce
            // read as a crouch twitch. Fan on the tuck.
            float hopTail = WalkHopTail01();
            if (hopTail > 0.01f)
                z = Mathf.Max(z, 6f + 14f * hopTail);
        }
        else if (mode == BirdMode.Attack)
            z = -10f;
        else if (flight != null)
            z = 28f * flight.flareLook - 16f * flight.swoopLook;
        // Brain already wrote Fly. Coil, trail, and the first stroke
        // were live; forage Fly left the tail on cruise, so the hop
        // read as wander. Landing flare and Alarm keep their own fans.
        float takeoff = TakeoffTailFan();
        if (takeoff > 0.01f)
            z = Mathf.Max(z, 24f * takeoff);
        // Brain already wrote SetFacing. Wings and neck bank; the
        // fan still rode airTurnAmp, so the about-face sat on the
        // bank envelope. Independently gated. Sit hop-turn owns
        // sitTurnTailAmp. Walk hop-turn owns walkTurnTailAmp.
        // Takeoff / landing flare / Alarm keep their own fans.
        float airTail = AirTurnTail01();
        if (airTail > 0.01f)
            z = Mathf.Max(z, 18f * airTail);
        // Brain already posted Alarm then Fly/Walk. Cry opened the beak;
        // forage Fly left the tail on cruise, so Flee read as wander.
        // Sit freeze independently gates a startle drop
        // (`alarmLookSitTailAmp`); without it Sit still holds the
        // pad crouch drop / dirt 6° under this Fly/Walk Max.
        float look = AlarmLook();
        if (look > 0.01f)
            z = Mathf.Max(z, 22f * look);
        // BIRD-6 Flee Sit: extra plus (same sign as pad crouch)
        // after hitch/crouch/grasp/hunch. Fly/Walk keep the 22°
        // fan. Land ruffle / hop-turn skip (AlarmLookSitTail01).
        float sitTail = AlarmLookSitTail01();
        if (sitTail > 0.01f)
            z += 12f * sitTail;
        if (z == lastTailZ)
            return;
        bird.tailVisual.localRotation = Quaternion.Euler(0f, 0f, z);
        lastTailZ = z;
    }

    // Sit forage blink. Brain already writes Sit/Walk/Peck look;
    // without a pupil that is a blank yellow box. Night roost holds
    // shut. Fly/Glide/Attack keep a stare. Sit freeze independently
    // skips the 2–5 s shut (`alarmLookSitBlinkAmp`); without it Flee
    // still flashed a commute lid on a startled perch, including
    // hop-turn about-face (`AlarmLookSit` zeroes). Walk scramble
    // independently skips the same shut (`alarmLookWalkBlinkAmp`);
    // without it Flee still flashed a commute lid on a dirt dash,
    // including hop-turn about-face (`AlarmLookLeg` zeroes).
    // Peck keeps the forage clock. Cached SpriteRenderer on Bird,
    // no Find. 0 hides the eye.
    private void ApplySitBlink(bool rest)
    {
        if (bird == null || bird.eyeRenderer == null)
            return;
        SpriteRenderer eye = bird.eyeRenderer;
        if (sitBlinkAmp < 0.01f)
        {
            SetEyeOn(eye, false);
            return;
        }

        bool roostShut = NightRoost() > 0.35f;
        bool forage = mode == BirdMode.Sit || mode == BirdMode.Walk
            || mode == BirdMode.Peck || mode == BirdMode.Stand;
        float now = Time.time;
        if (rest || roostShut)
        {
            blinkUntil = 0f;
            if (rest)
                nextBlinkAt = now + 0.8f;
            SetEyeOn(eye, !roostShut);
            return;
        }

        if (!forage)
        {
            blinkUntil = 0f;
            SetEyeOn(eye, true);
            return;
        }

        // BIRD-6 Flee Sit: hold the stare through the glance after
        // cry. Hitch/crouch/grasp/hunch/tail already freeze; Sit
        // still blinked 0.12 s every 2–5 s, so Flee still flashed
        // a commute lid. Eye on. Night roost already held shut
        // above. Hop-turn about-face zeroes AlarmLookSit; hold
        // the stare through the plant (AlarmLookSitBlink01).
        // 0 = old Sit blink.
        if (AlarmLookSitBlink01() > 0.01f)
        {
            blinkUntil = 0f;
            nextBlinkAt = now + 0.8f;
            SetEyeOn(eye, true);
            return;
        }

        // BIRD-6 Flee Walk: hold the stare through the glance after
        // cry. Hitch/scramble already dash; Walk still blinked
        // 0.12 s every 2–5 s, so Flee still flashed a commute lid
        // on the dirt. Eye on. Sit freeze stays Sit. Peck keeps
        // the forage clock. Night roost already held shut above.
        // Hop-turn about-face zeroes AlarmLookLeg; hold the stare
        // through the plant (AlarmLookWalkBlink01). 0 = old Walk
        // blink.
        if (AlarmLookWalkBlink01() > 0.01f)
        {
            blinkUntil = 0f;
            nextBlinkAt = now + 0.8f;
            SetEyeOn(eye, true);
            return;
        }

        if (now >= nextBlinkAt)
        {
            blinkUntil = now + 0.12f;
            float u = Mathf.Abs(Mathf.Sin(now * 12.9898f + GetHashCode()));
            nextBlinkAt = now + 2.2f + u * 2.4f;
        }

        SetEyeOn(eye, now >= blinkUntil);
    }

    // Attack stoop: cover the pupil. Picture only — BeakStrike still
    // reads the trigger. Fly/Glide keep the stare. Sit blink stays
    // the forage lid. Cached SpriteRenderer on Bird, no Find.
    // 0 = old Fly stare (stoopNictAmp 0).
    private void ApplyStoopNict()
    {
        if (bird == null || bird.nictRenderer == null)
            return;
        SpriteRenderer nict = bird.nictRenderer;
        bool on = !energyRest
            && stoopNictAmp > 0.01f
            && mode == BirdMode.Attack;
        if (on == lastNictOn && nict.enabled == on)
            return;
        nict.enabled = on;
        lastNictOn = on;
    }

    private void SetEyeOn(SpriteRenderer eye, bool on)
    {
        if (on == lastEyeOn && eye.enabled == on)
            return;
        eye.enabled = on;
        lastEyeOn = on;
    }

    // Dirt Walk / Peck forage / Sit preen / Sit scratch / Sit look /
    // Walk hop-turn / Sit hop-turn / Fly-Glide air turn / landing
    // flare / dirt swoop / takeoff climb / Sit land-ruffle: drop the pupil toward the soil, the bill, the kicking
    // foot, the perch, the bank, the plant, the first-air climb, the plant ruffle, or glance it with
    // idleLook. Picture only — Sit blink still owns the shut.
    // Neck dip already holds the beak down; the rest pupil rode
    // the stride bob back to ahead, so forage still flashed a
    // commute stare. Peck lid slits the jab; swallow still opened
    // onto rest-Y, so the toss was an ahead stare. Sit idlePreen
    // already slits and nibbles; rest-Y made grooming a horizon
    // stare. Sit idleHop already slits; rest-Y made scratch a
    // horizon kick. Sit idleLook already turns the neck; rest-Y
    // stayed socket-forward, so look-around read as a nodding
    // statue eye. Walk hop-turn already hops legs, neck, tail,
    // and wings; Walk scan stayed skipped, so the about-face
    // flashed a horizon stare. Walk hop-turn lid slits that drop
    // (`walkTurnLidAmp`). Sit hop-turn already hops the same
    // limbs; Sit look / preen / scratch stayed skipped, so the
    // perch about-face flashed a horizon stare (`sitTurnEyeAmp`).
    // Sit hop-turn lid slits that drop (`sitTurnLidAmp`). Fly/Glide
    // SetFacing already banks wings, neck, and tail; the cruise
    // stare stayed rest-Y, so the about-face flashed a horizon
    // cruise (`airTurnEyeAmp`). Air-turn lid slits that drop
    // (`airTurnLidAmp`). Landing flare drops toward the plant
    // (`landEyeAmp`). Landing-flare lid slits that drop
    // (`landLidAmp`). Landing-flare gape parts that
    // drop (`landGapeAmp`). Dirt swoop drops toward the
    // soil (`swoopEyeAmp`). Dirt-swoop lid slits that
    // drop (`swoopLidAmp`). Dirt-swoop gape parts that
    // drop (`swoopGapeAmp`). Takeoff climb drops toward the
    // bill (`takeoffEyeAmp`). Takeoff-climb lid slits that
    // drop (`takeoffLidAmp`). Takeoff-climb gape parts that
    // drop (`takeoffGapeAmp`). Sit land-ruffle drops toward
    // the plant (`landSettleEyeAmp`). Sit land-ruffle lid slits
    // that drop (`landSettleLidAmp`). Sit land-ruffle gape
    // parts that drop (`landSettleGapeAmp`). Alarm glance drops
    // toward the bill (`alarmLookEyeAmp`). Alarm glance lid
    // slits that drop (`alarmLookLidAmp`). Alarm glance gape
    // parts that drop (`alarmLookGapeAmp`). Attack nict stays the pale cover.
    // Cached Transform on Bird, no Find.
    // 0 = old rest pupil.
    private void ApplyWalkScanEye()
    {
        if (bird == null || bird.eyeRenderer == null)
            return;
        float a = WalkScanEye01();
        if (a < 0.01f)
            a = PeckScanEye01();
        if (a < 0.01f)
            a = SitPreenEye01();
        if (a < 0.01f)
            a = SitScratchEye01();
        if (a < 0.01f)
            a = WalkTurnEye01();
        if (a < 0.01f)
            a = SitTurnEye01();
        if (a < 0.01f)
            a = AirTurnEye01();
        if (a < 0.01f)
            a = LandEye01();
        if (a < 0.01f)
            a = SwoopEye01();
        if (a < 0.01f)
            a = TakeoffEye01();
        if (a < 0.01f)
            a = LandSettleEye01();
        if (a < 0.01f)
            a = AlarmLookEye01();
        Vector3 pos = bird.eyeRestLocal;
        float hy = bird.headSize.y;
        if (a > 0.01f)
            pos += new Vector3(hy * 0.08f * a, -hy * 0.22f * a, 0f);
        else
        {
            float look = SitLookEye01();
            if (Mathf.Abs(look) > 0.01f)
                pos += new Vector3(-hy * 0.08f * look, hy * 0.22f * look, 0f);
        }
        if ((pos - lastEyeLocal).sqrMagnitude < 1e-12f)
            return;
        bird.eyeRenderer.transform.localPosition = pos;
        lastEyeLocal = pos;
    }

    private float WalkScanEye01()
    {
        if (walkEyeAmp < 0.01f || mode != BirdMode.Walk)
            return 0f;
        if (energyRest)
            return 0f;
        if (sensors != null && !sensors.bothGrounded)
            return 0f;
        if (WalkTurnHop() > 0.01f)
            return 0f;
        if (AlarmLook() > 0.01f)
            return 0f;
        return Mathf.Clamp(walkEyeAmp, 0f, 1.5f);
    }

    // Walk hop-turn: drop the same pupil toward the dirt. Picture
    // only — Walk scan / Peck / Sit-preen / Sit-scratch / Sit-look
    // eyes stay their modes. Attack nict stays the pale cover. Sit
    // blink still owns the shut. Wander already wrote SetFacing;
    // legs, neck, tail, and wings hop, Walk scan stayed skipped, so
    // the about-face still flashed a horizon stare. Hold the forage
    // drop through the plant (same slot as walkEyeAmp), not a pulse —
    // sin edges would snap rest-Y twice. Sit hop-turn owns
    // sitTurnEyeAmp on the perch. Air turn owns airTurnEyeAmp.
    // Stride scan stays Walk. Peck forage drop stays Peck.
    // Alarm glance skip. Lid rides the child (`walkTurnLidAmp`).
    // No Find. 0 = old rest pupil (walkTurnEyeAmp 0).
    private float WalkTurnEye01()
    {
        if (walkTurnEyeAmp < 0.01f || mode != BirdMode.Walk)
            return 0f;
        if (energyRest)
            return 0f;
        if (WalkTurnHop() < 0.01f)
            return 0f;
        if (AlarmLook() > 0.01f)
            return 0f;
        return Mathf.Clamp(walkTurnEyeAmp, 0f, 1.5f);
    }

    // Sit hop-turn: drop the same pupil toward the perch. Picture
    // only — Walk / Peck / Sit-preen / Sit-scratch / Sit-look eyes
    // stay their modes. Attack nict stays the pale cover. Sit
    // blink still owns the shut. Wander already wrote SetFacing;
    // legs, neck, tail, and wings hop, Sit look / preen / scratch
    // stayed skipped, so the about-face still flashed a horizon
    // stare. Hold the forage drop through the plant (same slot as
    // walkEyeAmp), not a pulse — sin edges would snap rest-Y twice.
    // Walk hop-turn stays on the dirt. Air turn owns airTurnEyeAmp.
    // Look / preen / scratch drops stay theirs. Alarm glance skip.
    // Lid rides the child (`sitTurnLidAmp`). No Find. 0 = old rest
    // pupil (sitTurnEyeAmp 0).
    private float SitTurnEye01()
    {
        if (sitTurnEyeAmp < 0.01f || mode != BirdMode.Sit)
            return 0f;
        if (energyRest)
            return 0f;
        if (SitTurnHop() < 0.01f)
            return 0f;
        if (AlarmLook() > 0.01f)
            return 0f;
        return Mathf.Clamp(sitTurnEyeAmp, 0f, 1.5f);
    }

    // Fly/Glide SetFacing: drop the same pupil toward the bill.
    // Picture only — Walk / Peck / Sit-preen / Sit-scratch /
    // Sit-look / Sit hop-turn / Walk hop-turn eyes stay their
    // modes. Attack nict stays the pale cover. Sit blink still
    // owns the shut. Wander already wrote SetFacing; wings, neck,
    // and tail bank, Fly/Glide stare stayed rest-Y, so the
    // about-face still flashed a cruise horizon. Hold the forage
    // drop through the bank (same slot as walkEyeAmp), not a
    // pulse — sin edges would snap rest-Y twice. Dirt takeoff,
    // pad first-air trail, and Attack skip with AirTurn01.
    // Landing flare owns landEyeAmp. Dirt swoop owns
    // swoopEyeAmp. Takeoff climb owns takeoffEyeAmp.
    // Sit ruffle owns landSettleEyeAmp.
    // Alarm glance skip. Lid
    // rides the child (`airTurnLidAmp`). Landing-flare lid
    // owns landLidAmp. Dirt-swoop lid owns swoopLidAmp.
    // Takeoff-climb lid owns takeoffLidAmp.
    // Sit ruffle lid owns landSettleLidAmp.
    // Landing-flare gape owns
    // landGapeAmp. Dirt-swoop gape owns
    // swoopGapeAmp. Takeoff-climb gape owns
    // takeoffGapeAmp. No Find. 0 = old cruise stare
    // (airTurnEyeAmp 0).
    private float AirTurnEye01()
    {
        if (airTurnEyeAmp < 0.01f)
            return 0f;
        if (mode != BirdMode.Fly && mode != BirdMode.Glide)
            return 0f;
        if (energyRest)
            return 0f;
        if (AirTurn01() < 0.01f)
            return 0f;
        if (AlarmLook() > 0.01f)
            return 0f;
        return Mathf.Clamp(airTurnEyeAmp, 0f, 1.5f);
    }

    // Fly/Glide flare: drop the same pupil toward the plant.
    // Picture only — Walk / Peck / Sit-preen / Sit-scratch /
    // Sit-look / Sit hop-turn / Walk hop-turn / air-turn eyes
    // stay their modes. Attack nict stays the pale cover. Sit
    // blink still owns the shut. Gear, wing cup, and tail fan
    // already read the flare; Fly/Glide stare stayed rest-Y, so
    // the plant still flashed a cruise horizon. Scale with
    // flareLook (closer is stronger), not a pulse. Skip climb
    // and dirt takeoff (vy and trail). Takeoff climb owns
    // takeoffEyeAmp. Swoop above flare owns
    // swoopEyeAmp. Air turn owns airTurnEyeAmp.
    // Sit ruffle owns landSettleEyeAmp. Alarm glance skip.
    // Lid rides the child (`landLidAmp`). Dirt-swoop lid
    // owns swoopLidAmp. Takeoff-climb lid owns takeoffLidAmp.
    // Sit ruffle lid owns landSettleLidAmp.
    // Gape owns
    // landGapeAmp. Dirt-swoop gape owns
    // swoopGapeAmp. Takeoff-climb gape owns
    // takeoffGapeAmp. No Find. 0 = old cruise stare (landEyeAmp 0).
    private float LandEye01()
    {
        if (landEyeAmp < 0.01f)
            return 0f;
        if (mode != BirdMode.Fly && mode != BirdMode.Glide)
            return 0f;
        if (energyRest)
            return 0f;
        if (OnDirtSupport())
            return 0f;
        if (TakeoffWing01() > 0.01f || TakeoffTrail01() > 0.01f)
            return 0f;
        if (bodyRb != null && bodyRb.linearVelocity.y > -0.08f)
            return 0f;
        if (AlarmLook() > 0.01f)
            return 0f;
        if (flight == null)
            return 0f;
        float t = flight.flareLook;
        if (t < 0.02f)
            return 0f;
        return Mathf.Clamp(landEyeAmp, 0f, 1.5f) * t;
    }

    // Fly/Glide dirt swoop: drop the same pupil toward the soil.
    // Picture only — Walk / Peck / Sit-preen / Sit-scratch /
    // Sit-look / Sit hop-turn / Walk hop-turn / air-turn / landing
    // flare eyes stay their modes. Attack nict stays the pale
    // cover. Sit blink still owns the shut. Wing spread and tail
    // stream already read the swoop; Fly/Glide stare stayed
    // rest-Y, so the dive still flashed a cruise horizon. Scale
    // with swoopLook (mid-window is strongest), not a pulse.
    // Skip climb and dirt takeoff (vy and trail). Takeoff
    // climb owns takeoffEyeAmp. Flare owns
    // landEyeAmp. Air turn owns airTurnEyeAmp. Alarm glance
    // skip. Lid rides the child (`swoopLidAmp`). Takeoff-climb
    // lid owns takeoffLidAmp. Gape owns
    // swoopGapeAmp. Takeoff-climb gape owns
    // takeoffGapeAmp. No Find.
    // 0 = old cruise stare (swoopEyeAmp 0).
    private float SwoopEye01()
    {
        if (swoopEyeAmp < 0.01f)
            return 0f;
        if (mode != BirdMode.Fly && mode != BirdMode.Glide)
            return 0f;
        if (energyRest)
            return 0f;
        if (OnDirtSupport())
            return 0f;
        if (TakeoffWing01() > 0.01f || TakeoffTrail01() > 0.01f)
            return 0f;
        if (bodyRb != null && bodyRb.linearVelocity.y > -0.08f)
            return 0f;
        if (AlarmLook() > 0.01f)
            return 0f;
        if (flight == null)
            return 0f;
        float t = flight.swoopLook;
        if (t < 0.02f)
            return 0f;
        return Mathf.Clamp(swoopEyeAmp, 0f, 1.5f) * t;
    }

    // Fly first air: drop the same pupil toward the bill.
    // Picture only — Walk / Peck / Sit-preen / Sit-scratch /
    // Sit-look / Sit hop-turn / Walk hop-turn / air-turn /
    // landing-flare / dirt-swoop eyes stay their modes. Attack
    // nict stays the pale cover. Sit blink still owns the shut.
    // Legs, wings, and tail already read the climb; Fly stare
    // stayed rest-Y, so the hop still flashed a cruise horizon.
    // Scale with the first-air clock (leave is strongest), not a
    // pulse. Dirt coil skip. Sinking skip (flare / swoop own
    // that). Flare owns landEyeAmp. Swoop owns swoopEyeAmp.
    // Air turn owns airTurnEyeAmp. Sit ruffle owns
    // landSettleEyeAmp. Alarm glance skip. Lid
    // rides the child (`takeoffLidAmp`). Sit ruffle lid
    // owns landSettleLidAmp. Gape rides the same
    // window (`takeoffGapeAmp`). No Find.
    // 0 = old cruise stare (takeoffEyeAmp 0).
    private float TakeoffEye01()
    {
        if (takeoffEyeAmp < 0.01f || mode != BirdMode.Fly)
            return 0f;
        if (energyRest)
            return 0f;
        if (OnDirtSupport())
            return 0f;
        if (bodyRb != null && bodyRb.linearVelocity.y < -0.08f)
            return 0f;
        if (AlarmLook() > 0.01f)
            return 0f;
        if (Time.time >= takeoffEyeUntil)
            return 0f;
        float dur = takeoffEyeUntil - takeoffEyeBorn;
        if (dur < 0.05f)
            return 0f;
        return Mathf.Clamp(takeoffEyeAmp, 0f, 1.5f)
            * (1f - Mathf.Clamp01((Time.time - takeoffEyeBorn) / dur));
    }

    // Sit after Fly/Glide/Attack: drop the same pupil toward the
    // plant. Picture only — Walk / Peck / Sit-preen / Sit-scratch /
    // Sit-look / Sit hop-turn / Walk hop-turn / air-turn /
    // landing-flare / dirt-swoop / takeoff-climb eyes stay their
    // modes. Attack nict stays the pale cover. Sit blink still
    // owns the shut. Wings and neck already ruffle; Sit look /
    // preen / scratch skip the window, so the plant still flashed
    // a Sit-cruise rest-Y. Scale with the settle clock (plant is
    // strongest), not a pulse. Night roost skip (LandSettle01).
    // Hop-turn skip. Flare owns landEyeAmp (Fly/Glide). Swoop
    // owns swoopEyeAmp. Takeoff climb owns takeoffEyeAmp. Air
    // turn owns airTurnEyeAmp. Alarm glance skip. Lid rides
    // the child (`landSettleLidAmp`). Gape rides the same
    // window (`landSettleGapeAmp`). No Find.
    // 0 = old Sit-cruise stare (landSettleEyeAmp 0).
    private float LandSettleEye01()
    {
        if (landSettleEyeAmp < 0.01f || mode != BirdMode.Sit)
            return 0f;
        if (energyRest)
            return 0f;
        if (SitTurnHop() > 0.01f)
            return 0f;
        if (AlarmLook() > 0.01f)
            return 0f;
        float t = LandSettle01();
        if (t < 0.02f)
            return 0f;
        float settle = Mathf.Max(0.01f, Mathf.Clamp(landSettleAmp, 0f, 1.5f));
        return Mathf.Clamp(landSettleEyeAmp, 0f, 1.5f) * (t / settle);
    }

    // BIRD-6 Flee: drop the same pupil toward the bill. Picture
    // only — Walk / Peck / Sit-preen / Sit-scratch / Sit-look /
    // Sit hop-turn / Walk hop-turn / air-turn / landing-flare /
    // dirt-swoop / takeoff-climb / Sit land-ruffle eyes stay their
    // modes. Attack nict stays the pale cover. Sit blink still
    // owns the shut. Neck and tail already glance; Fly/Walk stare
    // stayed rest-Y, so Flee still flashed a forage horizon.
    // Same Walk/Peck slot toward the bill. Scale with AlarmLook
    // (the glance envelope), not a pulse. Night roost skip.
    // Cry keeps Alarm gape. Lid rides the child (`alarmLookLidAmp`).
    // Gape rides the same window (`alarmLookGapeAmp`).
    // Other Eye01 skip while AlarmLook is live. No Find.
    // 0 = old forage stare (alarmLookEyeAmp 0).
    private float AlarmLookEye01()
    {
        if (alarmLookEyeAmp < 0.01f)
            return 0f;
        if (energyRest)
            return 0f;
        if (NightRoost() > 0.35f)
            return 0f;
        float look = AlarmLook();
        if (look < 0.02f)
            return 0f;
        float a = Mathf.Max(0.01f, Mathf.Clamp(alarmLookAmp, 0f, 1.5f));
        return Mathf.Clamp(alarmLookEyeAmp, 0f, 1.5f) * (look / a);
    }

    // BIRD-6 Flee: slit the same lid over that bill drop. Picture
    // only — Walk / Peck / Sit-preen / Sit-scratch / Sit-look /
    // Sit hop-turn / Walk hop-turn / air-turn / landing-flare /
    // dirt-swoop / takeoff-climb / Sit land-ruffle lids stay their
    // modes. Attack nict stays the pale cover. Sit blink still
    // owns the shut. Neck, tail, and pupil already glance;
    // Fly/Walk lid stayed skipped, so Flee still flashed a
    // round forage stare on the drop. Same Walk lid over the
    // pupil. Scale with AlarmLook (the glance envelope), not a
    // pulse. Night roost skip. Cry keeps Alarm gape. Gape
    // rides the same window (`alarmLookGapeAmp`). Other Lid01
    // skip while AlarmLook is live. No Find.
    // 0 = old forage stare (alarmLookLidAmp 0).
    private float AlarmLookLid01()
    {
        if (alarmLookLidAmp < 0.01f)
            return 0f;
        if (energyRest)
            return 0f;
        if (NightRoost() > 0.35f)
            return 0f;
        float look = AlarmLook();
        if (look < 0.02f)
            return 0f;
        float a = Mathf.Max(0.01f, Mathf.Clamp(alarmLookAmp, 0f, 1.5f));
        return Mathf.Clamp(alarmLookLidAmp, 0f, 1.5f) * (look / a);
    }

    // BIRD-6 Flee: part the same bill through the glance after
    // cry. Picture only — Walk / Peck / Sit-preen / Sit-scratch /
    // Sit-look / Sit hop-turn / Walk hop-turn / air-turn /
    // landing-flare / dirt-swoop / takeoff-climb / Sit land-ruffle
    // gapes stay their modes. Attack stoop stays Attack. Peck Cos
    // stays Peck. Neck, tail, pupil, and lid already glance;
    // Fly/Walk gape stayed skipped (WalkScanGape01 / AirTurnGape01
    // skip AlarmLook), so Flee still flashed a shut forage bill
    // after the 0.55 s cry on the 1.15 s glance. Same Walk part
    // (~8°). Scale with AlarmLook (the glance envelope), not a
    // pulse. Night roost skip. Cry keeps Alarm (the 0.55 s
    // shout — callers check CryGape first). Wing hitch is
    // independently gated (`alarmLookWingAmp`). Wattle hang is
    // independently gated (`alarmLookWattleAmp`). Comb nod is
    // independently gated (`alarmLookCombAmp`). Other Gape01 skip
    // while AlarmLook is live. No Find.
    // 0 = old forage shut (alarmLookGapeAmp 0).
    private float AlarmLookGape01()
    {
        if (alarmLookGapeAmp < 0.01f)
            return 0f;
        if (energyRest)
            return 0f;
        if (NightRoost() > 0.35f)
            return 0f;
        float look = AlarmLook();
        if (look < 0.02f)
            return 0f;
        float a = Mathf.Max(0.01f, Mathf.Clamp(alarmLookAmp, 0f, 1.5f));
        return Mathf.Clamp(alarmLookGapeAmp, 0f, 1.5f) * (look / a);
    }

    // BIRD-6 Flee: pin Fly / hitch Walk through the glance after
    // cry. Picture only — DriveWings still holds ragdoll pose.
    // Lift stays in BirdFlight. EvaluateFlap still clocks lift.
    // Takeoff / landing flare / Attack pin keep their own wings.
    // Walk stride / hen hop skip while AlarmLook is live.
    // Neck, tail, pupil, lid, and bill already glance; Fly still
    // beat cruise, so Flee still flashed a forage stroke after
    // the 0.55 s cry on the 1.15 s glance. Fold −90 is along
    // the back; slight plus presses the Fly box (stoop pin),
    // not preen. Walk hitch is the hop shrug from the fold.
    // Scale with AlarmLook (the glance envelope), not a pulse.
    // Night roost skip. Cry keeps Alarm. Other Wing01 skip
    // while AlarmLook is live. No Find.
    // 0 = old cruise flap (alarmLookWingAmp 0).
    private float AlarmLookWing01()
    {
        if (alarmLookWingAmp < 0.01f)
            return 0f;
        if (mode != BirdMode.Fly && mode != BirdMode.Walk)
            return 0f;
        if (energyRest)
            return 0f;
        if (NightRoost() > 0.35f)
            return 0f;
        float look = AlarmLook();
        if (look < 0.02f)
            return 0f;
        float a = Mathf.Max(0.01f, Mathf.Clamp(alarmLookAmp, 0f, 1.5f));
        return Mathf.Clamp(alarmLookWingAmp, 0f, 1.5f) * (look / a);
    }

    // BIRD-6 Flee Walk: scramble the dirt gait through the glance
    // after cry. Picture only — DriveLegs still holds ragdoll pose.
    // walkThrust still translates (do not pause like WalkTurnHop).
    // Hop-turn keeps the about-face. Fly tuck stays Fly. Neck,
    // tail, pupil, lid, bill, and wings already glance; Walk still
    // forage-strode / hopped, so Flee still flashed a commute
    // after the 0.55 s cry on the 1.15 s glance. Faster than
    // walkFrequency (scramble, not a new Hz field). Scale with
    // AlarmLook (the glance envelope), not a pulse. Night roost
    // skip. Cry keeps Alarm. Other Walk gait skip while live.
    // Walk startle blink is independently gated
    // (`alarmLookWalkBlinkAmp`); Walk still forage-blinked
    // 2–5 s. Hop-turn about-face also holds (AlarmLookLeg
    // zeroes). No Find. 0 = old forage stride/hop
    // (alarmLookLegAmp 0).
    private float AlarmLookLeg01()
    {
        if (alarmLookLegAmp < 0.01f)
            return 0f;
        if (mode != BirdMode.Walk)
            return 0f;
        if (energyRest)
            return 0f;
        if (NightRoost() > 0.35f)
            return 0f;
        if (WalkTurnHop() > 0.01f)
            return 0f;
        if (sensors != null && !sensors.bothGrounded)
            return 0f;
        float look = AlarmLook();
        if (look < 0.02f)
            return 0f;
        float a = Mathf.Max(0.01f, Mathf.Clamp(alarmLookAmp, 0f, 1.5f));
        return Mathf.Clamp(alarmLookLegAmp, 0f, 1.5f) * (look / a);
    }

    // BIRD-6 Flee Sit: freeze look/preen/scratch through the glance
    // after cry. Picture only — DriveLegs still holds ragdoll pose.
    // Neck, tail, pupil, lid, and bill already glance; Sit still
    // forage-looked / preened / scratched, so Flee still flashed
    // a perch commute after the 0.55 s cry on the 1.15 s glance
    // until Walk/Fly. Sit startle hitch is independently gated
    // (`alarmLookSitWingAmp`); Fly/Walk own the pin / hitch.
    // Sit startle crouch is independently gated
    // (`alarmLookSitLegAmp`); Walk scramble stays Walk.
    // Sit startle grasp is independently gated
    // (`alarmLookSitGraspAmp`); pad wrap / dirt 0 stay plant.
    // Sit startle hunch is independently gated
    // (`alarmLookSitHunchAmp`); pad retract / dirt 0 stay
    // silhouette. Sit startle tail is independently gated
    // (`alarmLookSitTailAmp`); pad crouch drop / dirt 6°
    // stay Sit under the Fly/Walk fan. Sit startle blink is
    // independently gated (`alarmLookSitBlinkAmp`); Sit
    // still forage-blinked 2–5 s, including hop-turn
    // about-face (AlarmLookSit zeroes). Walk startle blink
    // is independently gated (`alarmLookWalkBlinkAmp`); Walk
    // still forage-blinked 2–5 s, including hop-turn
    // about-face (AlarmLookLeg zeroes). Hop-turn keeps the
    // about-face. Night roost skip. Cry keeps Alarm. Other
    // Sit idle skip while live.
    // No Find. 0 = old forage fidget (alarmLookSitAmp 0).
    private float AlarmLookSit01()
    {
        if (alarmLookSitAmp < 0.01f)
            return 0f;
        if (mode != BirdMode.Sit)
            return 0f;
        if (energyRest)
            return 0f;
        if (NightRoost() > 0.35f)
            return 0f;
        if (SitTurnHop() > 0.01f)
            return 0f;
        float look = AlarmLook();
        if (look < 0.02f)
            return 0f;
        float a = Mathf.Max(0.01f, Mathf.Clamp(alarmLookAmp, 0f, 1.5f));
        return Mathf.Clamp(alarmLookSitAmp, 0f, 1.5f) * (look / a);
    }

    // BIRD-6 Flee Sit: hitch both wings off the Sit fold through the
    // glance after cry. Picture only — DriveWings still holds ragdoll
    // pose. Lift stays in BirdFlight. Neck, tail, pupil, lid, bill,
    // and freeze already glance; Sit still tucked, so Flee still
    // flashed a folded perch after the 0.55 s cry on the 1.15 s
    // glance until Walk/Fly. Plus from −90 (hop shrug / Walk hitch),
    // not preen. Scale with AlarmLookSit. Hop-turn keeps the
    // about-face. Land ruffle keeps the plant. Night roost skip.
    // Cry keeps Alarm. Fly pin / Walk hitch stay theirs. Sit
    // startle hunch is independently gated (`alarmLookSitHunchAmp`).
    // Sit startle tail is independently gated
    // (`alarmLookSitTailAmp`). Sit startle blink is
    // independently gated (`alarmLookSitBlinkAmp`).
    // No Find. 0 = old Sit fold (alarmLookSitWingAmp 0).
    private float AlarmLookSitWing01()
    {
        if (alarmLookSitWingAmp < 0.01f)
            return 0f;
        if (AlarmLookSit01() < 0.01f)
            return 0f;
        if (LandSettle01() > 0.01f)
            return 0f;
        float look = AlarmLook();
        if (look < 0.02f)
            return 0f;
        float a = Mathf.Max(0.01f, Mathf.Clamp(alarmLookAmp, 0f, 1.5f));
        return Mathf.Clamp(alarmLookSitWingAmp, 0f, 1.5f) * (look / a);
    }

    // BIRD-6 Flee Sit: crouch both legs off the Sit plant through the
    // glance after cry. Picture only — DriveLegs still holds ragdoll
    // pose. Neck, tail, pupil, lid, bill, freeze, and wing hitch
    // already glance; Sit still planted, so Flee still flashed a
    // perched sit after the 0.55 s cry on the 1.15 s glance until
    // Walk/Fly. Extra tuck from sitHip (same sign as pad crouch),
    // not a Walk scramble. Scale with AlarmLookSit. Hop-turn keeps
    // the about-face. Land ruffle keeps the plant. Night roost skip.
    // Cry keeps Alarm. Walk scramble stays Walk. Pad crouch stays
    // the twig rest. Sit startle grasp is independently gated
    // (`alarmLookSitGraspAmp`). Sit startle hunch is independently
    // gated (`alarmLookSitHunchAmp`). Sit startle tail is
    // independently gated (`alarmLookSitTailAmp`). Sit
    // startle blink is independently gated
    // (`alarmLookSitBlinkAmp`). No Find.
    // 0 = old Sit plant (alarmLookSitLegAmp 0).
    private float AlarmLookSitLeg01()
    {
        if (alarmLookSitLegAmp < 0.01f)
            return 0f;
        if (AlarmLookSit01() < 0.01f)
            return 0f;
        if (LandSettle01() > 0.01f)
            return 0f;
        float look = AlarmLook();
        if (look < 0.02f)
            return 0f;
        float a = Mathf.Max(0.01f, Mathf.Clamp(alarmLookAmp, 0f, 1.5f));
        return Mathf.Clamp(alarmLookSitLegAmp, 0f, 1.5f) * (look / a);
    }

    // BIRD-6 Flee Sit: clench both feet off the Sit plant through the
    // glance after cry. Picture only — DriveLegs still holds ragdoll
    // pose (ankles stay CoM). Neck, tail, pupil, lid, bill, freeze,
    // wing hitch, and startle crouch already glance; Sit still
    // planted the toes (pad wrap / dirt 0), so Flee still flashed a
    // perched sit after the 0.55 s cry on the 1.15 s glance until
    // Walk/Fly. Extra wrap (same sign as sitGraspAmp), not a Walk
    // toe-drop. Scale with AlarmLookSit. Hop-turn keeps the
    // about-face. Land ruffle keeps the plant. Night roost skip.
    // Cry keeps Alarm. Walk scramble stays Walk. Pad wrap stays
    // the twig rest. Sit startle hunch is independently gated
    // (`alarmLookSitHunchAmp`). Sit startle tail is independently
    // gated (`alarmLookSitTailAmp`). Sit startle blink is
    // independently gated (`alarmLookSitBlinkAmp`). No Find.
    // 0 = old Sit plant feet (alarmLookSitGraspAmp 0).
    private float AlarmLookSitGrasp01()
    {
        if (alarmLookSitGraspAmp < 0.01f)
            return 0f;
        if (AlarmLookSit01() < 0.01f)
            return 0f;
        if (LandSettle01() > 0.01f)
            return 0f;
        float look = AlarmLook();
        if (look < 0.02f)
            return 0f;
        float a = Mathf.Max(0.01f, Mathf.Clamp(alarmLookAmp, 0f, 1.5f));
        return Mathf.Clamp(alarmLookSitGraspAmp, 0f, 1.5f) * (look / a);
    }

    // BIRD-6 Flee Sit: hunch neck and tuck wings off the Sit
    // silhouette through the glance after cry. Picture only —
    // DriveNeck / DriveWings still hold ragdoll pose. Lift stays
    // in BirdFlight. Neck, tail, pupil, lid, bill, freeze, wing
    // hitch, startle crouch, and startle grasp already glance;
    // Sit still held the dirt-sit silhouette (pad retract / dirt
    // 0 extra), so Flee still flashed a perched sit after the
    // 0.55 s cry on the 1.15 s glance until Walk/Fly. Extra
    // retract (same sign as sitHunchAmp), not a Walk scan. Scale
    // with AlarmLookSit. Hop-turn keeps the about-face. Land
    // ruffle keeps the plant. Night roost skip. Cry keeps Alarm.
    // Walk scramble stays Walk. Pad hunch stays the twig rest.
    // Hitch stays the startle flash (extra tuck after hitch).
    // Sit startle tail is independently gated
    // (`alarmLookSitTailAmp`). Sit startle blink is
    // independently gated (`alarmLookSitBlinkAmp`).
    // No Find. 0 = old Sit silhouette (alarmLookSitHunchAmp 0).
    private float AlarmLookSitHunch01()
    {
        if (alarmLookSitHunchAmp < 0.01f)
            return 0f;
        if (AlarmLookSit01() < 0.01f)
            return 0f;
        if (LandSettle01() > 0.01f)
            return 0f;
        float look = AlarmLook();
        if (look < 0.02f)
            return 0f;
        float a = Mathf.Max(0.01f, Mathf.Clamp(alarmLookAmp, 0f, 1.5f));
        return Mathf.Clamp(alarmLookSitHunchAmp, 0f, 1.5f) * (look / a);
    }

    // BIRD-6 Flee Sit: drop the tail off the Sit rest through the
    // glance after cry. Picture only — DriveTail still holds
    // ragdoll pitch. Neck, pupil, lid, bill, freeze, Fly pin,
    // Walk hitch, Sit hitch, Sit crouch, Sit grasp, and Sit
    // hunch already glance; Sit still held the pad crouch drop
    // / dirt 6° under the Fly/Walk 22° Max, so Flee still
    // flashed a commute fan after the 0.55 s cry on the 1.15 s
    // glance until Walk/Fly. Extra plus (same sign as pad
    // crouch), not a Walk stride pump. Scale with AlarmLookSit.
    // Hop-turn keeps the about-face. Land ruffle keeps the
    // plant. Night roost skip. Cry keeps Alarm. Walk scramble
    // stays Walk. Pad crouch stays the twig rest. Hitch stays
    // the startle flash. Look / preen / scratch keep their own
    // fans. Sit startle blink is independently gated
    // (`alarmLookSitBlinkAmp`). No Find.
    // 0 = old Sit tail (alarmLookSitTailAmp 0).
    private float AlarmLookSitTail01()
    {
        if (alarmLookSitTailAmp < 0.01f)
            return 0f;
        if (AlarmLookSit01() < 0.01f)
            return 0f;
        if (LandSettle01() > 0.01f)
            return 0f;
        float look = AlarmLook();
        if (look < 0.02f)
            return 0f;
        float a = Mathf.Max(0.01f, Mathf.Clamp(alarmLookAmp, 0f, 1.5f));
        return Mathf.Clamp(alarmLookSitTailAmp, 0f, 1.5f) * (look / a);
    }

    // BIRD-6 Flee Sit: skip the forage blink through the glance
    // after cry. Picture only — ApplySitBlink still owns the
    // pupil. Neck, pupil, lid, bill, freeze, hitch, crouch,
    // grasp, hunch, and tail already glance; Sit still shut
    // 0.12 s every 2–5 s, so Flee still flashed a commute lid
    // after the 0.55 s cry on the 1.15 s glance until Walk/Fly.
    // Hold the stare (eye on). Scale with AlarmLook. Freeze or
    // hop-turn: AlarmLookSit zeroes on about-face, so the perch
    // dash still forage-blinked on SetFacing. Hop-turn owns the
    // plant; this only keeps the stare. Night roost skip (already
    // shut). Cry keeps Alarm. Walk scramble independently skips
    // (`alarmLookWalkBlinkAmp`), including hop-turn about-face.
    // Peck keeps the forage clock. Lid / pupil glance stay
    // alarmLookLid / Eye.
    // No Find. 0 = old Sit blink (alarmLookSitBlinkAmp 0).
    private float AlarmLookSitBlink01()
    {
        if (alarmLookSitBlinkAmp < 0.01f)
            return 0f;
        if (mode != BirdMode.Sit)
            return 0f;
        if (energyRest)
            return 0f;
        if (NightRoost() > 0.35f)
            return 0f;
        float look = AlarmLook();
        if (look < 0.02f)
            return 0f;
        if (AlarmLookSit01() < 0.01f && SitTurnHop() < 0.01f)
            return 0f;
        float a = Mathf.Max(0.01f, Mathf.Clamp(alarmLookAmp, 0f, 1.5f));
        return Mathf.Clamp(alarmLookSitBlinkAmp, 0f, 1.5f) * (look / a);
    }

    // BIRD-6 Flee Walk: skip the forage blink through the glance
    // after cry. Picture only — ApplySitBlink still owns the
    // pupil. Neck, pupil, lid, bill, Fly pin, Walk hitch, and
    // Walk scramble already glance; Walk still shut 0.12 s every
    // 2–5 s, so Flee still flashed a commute lid after the
    // 0.55 s cry on the 1.15 s glance. Hold the stare (eye on).
    // Scale with AlarmLook. Scramble or hop-turn: AlarmLookLeg
    // zeroes on about-face, so the dirt dash still forage-blinked
    // on SetFacing. Hop-turn owns the plant; this only keeps the
    // stare. Night roost skip (already shut). Cry keeps Alarm.
    // Sit freeze stays Sit. Sit hop-turn independently skips
    // (`alarmLookSitBlinkAmp`). Peck keeps the forage clock. Lid /
    // pupil glance stay alarmLookLid / Eye.
    // No Find. 0 = old Walk blink (alarmLookWalkBlinkAmp 0).
    private float AlarmLookWalkBlink01()
    {
        if (alarmLookWalkBlinkAmp < 0.01f)
            return 0f;
        if (mode != BirdMode.Walk)
            return 0f;
        if (energyRest)
            return 0f;
        if (NightRoost() > 0.35f)
            return 0f;
        float look = AlarmLook();
        if (look < 0.02f)
            return 0f;
        if (AlarmLookLeg01() < 0.01f && WalkTurnHop() < 0.01f)
            return 0f;
        float a = Mathf.Max(0.01f, Mathf.Clamp(alarmLookAmp, 0f, 1.5f));
        return Mathf.Clamp(alarmLookWalkBlinkAmp, 0f, 1.5f) * (look / a);
    }

    // Sit after Fly/Glide/Attack: slit the same lid over that plant
    // drop. Picture only — Walk / Peck / Sit-preen / Sit-scratch /
    // Sit hop-turn / Walk hop-turn / air-turn / landing-flare /
    // dirt-swoop / takeoff-climb lids stay their modes. Attack nict
    // stays the pale cover. Sit blink still owns the shut. Wings,
    // neck, and pupil already ruffle; Sit look / preen / scratch
    // skip the window, so the plant still flashed a round
    // Sit-cruise stare on the drop. Same Walk lid over the pupil.
    // Scale with the settle clock (plant is strongest), not a
    // pulse. Night roost skip (LandSettle01). Hop-turn skip.
    // Flare owns landLidAmp (Fly/Glide). Swoop owns swoopLidAmp.
    // Takeoff climb owns takeoffLidAmp. Air turn owns
    // airTurnLidAmp. Alarm glance skip. Gape rides
    // the same window (`landSettleGapeAmp`). No Find.
    // 0 = old Sit-cruise stare (landSettleLidAmp 0).
    private float LandSettleLid01()
    {
        if (landSettleLidAmp < 0.01f || mode != BirdMode.Sit)
            return 0f;
        if (energyRest)
            return 0f;
        if (SitTurnHop() > 0.01f)
            return 0f;
        if (AlarmLook() > 0.01f)
            return 0f;
        float t = LandSettle01();
        if (t < 0.02f)
            return 0f;
        float settle = Mathf.Max(0.01f, Mathf.Clamp(landSettleAmp, 0f, 1.5f));
        return Mathf.Clamp(landSettleLidAmp, 0f, 1.5f) * (t / settle);
    }

    // Sit after Fly/Glide/Attack: part the same bill toward the
    // plant. Picture only — Walk / Peck / Sit-preen / Attack keep
    // their own gape. Cry keeps Alarm. Wings, neck, pupil, and lid
    // already ruffle; Sit look / preen / scratch skip the window, so
    // the plant still flashed a shut Sit-cruise bill on the drop.
    // Same Walk part (~8°). Scale with the settle clock (plant is
    // strongest), not a pulse. Night roost skip (LandSettle01).
    // Hop-turn skip. Flare owns landGapeAmp (Fly/Glide). Swoop
    // owns swoopGapeAmp. Takeoff climb owns takeoffGapeAmp. Air
    // turn owns airTurnGapeAmp. Look-down owns sitLookGapeAmp.
    // Scratch owns sitScratchGapeAmp. Preen nibble stays preen.
    // Tail hitch is independently gated (landSettleTailAmp).
    // Wattle hang is independently gated (landSettleWattleAmp).
    // Comb nod is independently gated (landSettleCombAmp).
    // Alarm glance skip. No Find.
    // 0 = old Sit-cruise shut (landSettleGapeAmp 0).
    private float LandSettleGape01()
    {
        if (landSettleGapeAmp < 0.01f || mode != BirdMode.Sit)
            return 0f;
        if (energyRest)
            return 0f;
        if (SitTurnHop() > 0.01f)
            return 0f;
        if (AlarmLook() > 0.01f)
            return 0f;
        float t = LandSettle01();
        if (t < 0.02f)
            return 0f;
        float settle = Mathf.Max(0.01f, Mathf.Clamp(landSettleAmp, 0f, 1.5f));
        return Mathf.Clamp(landSettleGapeAmp, 0f, 1.5f) * (t / settle);
    }

    // Sit after Fly/Glide/Attack: hitch the tail with the ruffle.
    // Picture only — DriveTail still holds ragdoll pitch. Wings,
    // neck, pupil, lid, and bill already shake; the fan still rode
    // landSettleAmp (the old slow 18° envelope), so the plant still
    // sat on a Sit brake, not a ruffle. Plus drops the free end.
    // Hitch with the same 22 Hz rake as the wings, not a hold.
    // Look owns sitLookTailAmp. Preen owns sitPreenTailAmp.
    // Scratch owns sitScratchTailAmp. Hop-turn owns sitTurnTailAmp.
    // Walk hop-turn owns walkTurnTailAmp. Air turn owns
    // airTurnTailAmp. Takeoff owns takeoffTailAmp. Pad crouch /
    // Alarm keep their own fans. Night roost skip (LandSettle01).
    // Hop-turn skip. No Find.
    // 0 = old Sit drop (landSettleTailAmp 0).
    private float LandSettleTail01()
    {
        if (landSettleTailAmp < 0.01f || mode != BirdMode.Sit)
            return 0f;
        if (energyRest)
            return 0f;
        if (SitTurnHop() > 0.01f)
            return 0f;
        if (AlarmLook() > 0.01f)
            return 0f;
        float t = LandSettle01();
        if (t < 0.02f)
            return 0f;
        float settle = Mathf.Max(0.01f, Mathf.Clamp(landSettleAmp, 0f, 1.5f));
        return Mathf.Clamp(landSettleTailAmp, 0f, 1.5f) * (t / settle);
    }

    // Fly first air: slit the same lid over that bill drop.
    // Picture only — Walk / Peck / Sit-preen / Sit-scratch /
    // Sit hop-turn / Walk hop-turn / air-turn / landing-flare /
    // dirt-swoop lids stay their modes. Attack nict stays the
    // pale cover. Sit blink still owns the shut. Legs, wings,
    // tail, and pupil already read the climb; Fly lid stayed
    // skipped, so the hop still flashed a round cruise stare
    // on the drop. Same Walk lid over the pupil. Scale with
    // the first-air clock (leave is strongest), not a pulse.
    // Dirt coil skip. Sinking skip (flare / swoop own that).
    // Flare owns landLidAmp. Swoop owns swoopLidAmp. Air
    // turn owns airTurnLidAmp. Sit ruffle owns
    // landSettleLidAmp. Alarm glance skip. Gape rides
    // the same window (`takeoffGapeAmp`). No Find.
    // 0 = old cruise stare (takeoffLidAmp 0).
    private float TakeoffLid01()
    {
        if (takeoffLidAmp < 0.01f || mode != BirdMode.Fly)
            return 0f;
        if (energyRest)
            return 0f;
        if (OnDirtSupport())
            return 0f;
        if (bodyRb != null && bodyRb.linearVelocity.y < -0.08f)
            return 0f;
        if (AlarmLook() > 0.01f)
            return 0f;
        if (Time.time >= takeoffLidUntil)
            return 0f;
        float dur = takeoffLidUntil - takeoffLidBorn;
        if (dur < 0.05f)
            return 0f;
        return Mathf.Clamp(takeoffLidAmp, 0f, 1.5f)
            * (1f - Mathf.Clamp01((Time.time - takeoffLidBorn) / dur));
    }

    // Fly first air: part the same bill toward the climb.
    // Picture only — Walk / Peck / Sit-preen / Attack keep their
    // own gape. Cry keeps Alarm. Legs, wings, tail, pupil, and
    // lid already read the climb; Fly gape stayed skipped, so
    // the hop still flashed a shut cruise bill on the drop.
    // Same Walk part (~8°). Scale with the first-air clock
    // (leave is strongest), not a pulse. Dirt coil skip.
    // Sinking skip (flare / swoop own that). Flare owns
    // landGapeAmp. Swoop owns swoopGapeAmp. Air turn owns
    // airTurnGapeAmp. Sit ruffle owns landSettleGapeAmp.
    // Alarm glance skip. Wattle hang rides the same
    // window (`takeoffWattleAmp`). Comb nod rides the same
    // window (`takeoffCombAmp`).
    // No Find.
    // 0 = old cruise shut (takeoffGapeAmp 0).
    private float TakeoffGape01()
    {
        if (takeoffGapeAmp < 0.01f || mode != BirdMode.Fly)
            return 0f;
        if (energyRest)
            return 0f;
        if (OnDirtSupport())
            return 0f;
        if (bodyRb != null && bodyRb.linearVelocity.y < -0.08f)
            return 0f;
        if (AlarmLook() > 0.01f)
            return 0f;
        if (Time.time >= takeoffGapeUntil)
            return 0f;
        float dur = takeoffGapeUntil - takeoffGapeBorn;
        if (dur < 0.05f)
            return 0f;
        return Mathf.Clamp(takeoffGapeAmp, 0f, 1.5f)
            * (1f - Mathf.Clamp01((Time.time - takeoffGapeBorn) / dur));
    }

    // Fly/Glide flare: slit the same lid over that plant drop.
    // Picture only — Walk / Peck / Sit-preen / Sit-scratch /
    // Sit hop-turn / Walk hop-turn / air-turn lids stay their
    // modes. Attack nict stays the pale cover. Sit blink still
    // owns the shut. Gear, cup, tail, and pupil already read
    // the flare; Fly/Glide lid stayed skipped, so the plant
    // still flashed a round cruise stare on the drop. Same
    // Walk lid over the pupil. Scale with flareLook (closer
    // is stronger), not a pulse. Skip climb and dirt takeoff
    // (vy and trail). Takeoff climb owns takeoffLidAmp. Swoop
    // above flare owns swoopLidAmp. Air turn owns
    // airTurnLidAmp. Sit ruffle owns landSettleLidAmp.
    // Alarm glance skip. Gape
    // rides the same window (`landGapeAmp`). Dirt-swoop
    // gape owns swoopGapeAmp. Takeoff-climb gape owns
    // takeoffGapeAmp. No Find.
    // 0 = old cruise stare (landLidAmp 0).
    private float LandLid01()
    {
        if (landLidAmp < 0.01f)
            return 0f;
        if (mode != BirdMode.Fly && mode != BirdMode.Glide)
            return 0f;
        if (energyRest)
            return 0f;
        if (OnDirtSupport())
            return 0f;
        if (TakeoffWing01() > 0.01f || TakeoffTrail01() > 0.01f)
            return 0f;
        if (bodyRb != null && bodyRb.linearVelocity.y > -0.08f)
            return 0f;
        if (AlarmLook() > 0.01f)
            return 0f;
        if (flight == null)
            return 0f;
        float t = flight.flareLook;
        if (t < 0.02f)
            return 0f;
        return Mathf.Clamp(landLidAmp, 0f, 1.5f) * t;
    }

    // Fly/Glide dirt swoop: slit the same lid over that soil drop.
    // Picture only — Walk / Peck / Sit-preen / Sit-scratch /
    // Sit hop-turn / Walk hop-turn / air-turn / landing-flare
    // lids stay their modes. Attack nict stays the pale cover.
    // Sit blink still owns the shut. Wing spread, tail stream,
    // and pupil already read the swoop; Fly/Glide lid stayed
    // skipped, so the dive still flashed a round cruise stare
    // on the drop. Same Walk lid over the pupil. Scale with
    // swoopLook (mid-window is strongest), not a pulse. Skip
    // climb and dirt takeoff (vy and trail). Takeoff climb
    // owns takeoffLidAmp. Flare owns landLidAmp. Air turn
    // owns airTurnLidAmp. Sit ruffle owns landSettleLidAmp.
    // Alarm glance skip. Gape owns
    // swoopGapeAmp. Takeoff-climb gape owns takeoffGapeAmp. No Find.
    // 0 = old cruise stare (swoopLidAmp 0).
    private float SwoopLid01()
    {
        if (swoopLidAmp < 0.01f)
            return 0f;
        if (mode != BirdMode.Fly && mode != BirdMode.Glide)
            return 0f;
        if (energyRest)
            return 0f;
        if (OnDirtSupport())
            return 0f;
        if (TakeoffWing01() > 0.01f || TakeoffTrail01() > 0.01f)
            return 0f;
        if (bodyRb != null && bodyRb.linearVelocity.y > -0.08f)
            return 0f;
        if (AlarmLook() > 0.01f)
            return 0f;
        if (flight == null)
            return 0f;
        float t = flight.swoopLook;
        if (t < 0.02f)
            return 0f;
        return Mathf.Clamp(swoopLidAmp, 0f, 1.5f) * t;
    }

    // Fly/Glide SetFacing: slit the same lid over that bill drop.
    // Picture only — Walk / Peck / Sit-preen / Sit-scratch /
    // Sit hop-turn / Walk hop-turn lids stay their modes.
    // Attack nict stays the pale cover. Sit blink still owns
    // the shut. Wander already wrote SetFacing; wings, neck,
    // tail, and pupil bank, Fly/Glide lid stayed skipped, so
    // the about-face still flashed a round cruise stare on
    // the drop. Hold through the bank (same slot as
    // walkLidAmp), not a pulse — sin edges would snap the
    // round pupil twice. Sit hop-turn owns sitTurnLidAmp
    // on the perch. Walk hop-turn owns walkTurnLidAmp on
    // the dirt. Stride slit stays Walk. Peck jab stays Peck.
    // Dirt takeoff, pad first-air trail, landing flare, and
    // Attack skip with AirTurn01. Takeoff climb owns
    // takeoffLidAmp. Landing flare owns
    // landLidAmp. Dirt swoop owns swoopLidAmp. Alarm glance
    // skip. Gape rides the same
    // window (`airTurnGapeAmp`). Landing-flare gape owns
    // landGapeAmp. Dirt-swoop gape owns swoopGapeAmp.
    // Takeoff-climb gape owns takeoffGapeAmp. No Find.
    // 0 = old cruise stare (airTurnLidAmp 0).
    private float AirTurnLid01()
    {
        if (airTurnLidAmp < 0.01f)
            return 0f;
        if (mode != BirdMode.Fly && mode != BirdMode.Glide)
            return 0f;
        if (energyRest)
            return 0f;
        if (AirTurn01() < 0.01f)
            return 0f;
        if (AlarmLook() > 0.01f)
            return 0f;
        return Mathf.Clamp(airTurnLidAmp, 0f, 1.5f);
    }

    // Fly/Glide SetFacing: part the same bill toward the cruise.
    // Picture only — Walk / Peck / Sit-preen / Attack keep their
    // own gape. Cry keeps Alarm. Wander already wrote SetFacing;
    // wings, neck, tail, pupil, and lid bank, Fly/Glide gape
    // stayed skipped, so the about-face still flashed a shut
    // bill on the drop. Hold through the bank (same ~8° as
    // walkGapeAmp), not a pulse — sin edges would snap the
    // shut twice. Sit hop-turn owns sitTurnGapeAmp on the
    // perch. Walk hop-turn owns walkTurnGapeAmp on the dirt.
    // Stride gape stays Walk. Peck Cos stays Peck. Dirt
    // takeoff, pad first-air trail, landing flare, and Attack
    // skip with AirTurn01. Landing flare owns landGapeAmp.
    // Dirt swoop owns swoopGapeAmp. Takeoff climb owns
    // takeoffGapeAmp. Sit ruffle owns landSettleGapeAmp.
    // Alarm glance skip. No Find.
    // 0 = old cruise shut (airTurnGapeAmp 0).
    private float AirTurnGape01()
    {
        if (airTurnGapeAmp < 0.01f)
            return 0f;
        if (mode != BirdMode.Fly && mode != BirdMode.Glide)
            return 0f;
        if (energyRest)
            return 0f;
        if (AirTurn01() < 0.01f)
            return 0f;
        if (AlarmLook() > 0.01f)
            return 0f;
        return Mathf.Clamp(airTurnGapeAmp, 0f, 1.5f);
    }

    // Fly/Glide flare: part the same bill toward the plant.
    // Picture only — Walk / Peck / Sit-preen / Attack keep their
    // own gape. Cry keeps Alarm. Gear, cup, tail, pupil, and lid
    // already read the flare; Fly/Glide gape stayed skipped, so
    // the plant still flashed a shut cruise bill on the drop.
    // Same Walk part (~8°). Scale with flareLook (closer is
    // stronger), not a pulse. Skip climb and dirt takeoff (vy
    // and trail). Takeoff climb owns takeoffGapeAmp. Dirt swoop
    // owns swoopGapeAmp. Air turn owns airTurnGapeAmp. Sit
    // ruffle owns landSettleGapeAmp. Alarm glance skip. Wattle
    // hang is independently gated (`landWattleAmp`). Comb nod is
    // independently gated (`landCombAmp`). No Find.
    // 0 = old cruise shut (landGapeAmp 0).
    private float LandGape01()
    {
        if (landGapeAmp < 0.01f)
            return 0f;
        if (mode != BirdMode.Fly && mode != BirdMode.Glide)
            return 0f;
        if (energyRest)
            return 0f;
        if (OnDirtSupport())
            return 0f;
        if (TakeoffWing01() > 0.01f || TakeoffTrail01() > 0.01f)
            return 0f;
        if (bodyRb != null && bodyRb.linearVelocity.y > -0.08f)
            return 0f;
        if (AlarmLook() > 0.01f)
            return 0f;
        if (flight == null)
            return 0f;
        float t = flight.flareLook;
        if (t < 0.02f)
            return 0f;
        return Mathf.Clamp(landGapeAmp, 0f, 1.5f) * t;
    }

    // Fly/Glide dirt swoop: part the same bill toward the soil.
    // Picture only — Walk / Peck / Sit-preen / Attack keep their
    // own gape. Cry keeps Alarm. Wing, tail, pupil, and lid
    // already read the swoop; Fly/Glide gape stayed skipped, so
    // the dive still flashed a shut cruise bill on the drop.
    // Same Walk part (~8°). Scale with swoopLook (mid-window is
    // strongest), not a pulse. Skip climb and dirt takeoff (vy
    // and trail). Takeoff climb owns takeoffGapeAmp. Flare owns
    // landGapeAmp. Air turn owns airTurnGapeAmp. Sit ruffle
    // owns landSettleGapeAmp. Alarm glance skip. Talon hang is
    // independently gated (`swoopTalonAmp`). Tongue is independently
    // gated (`swoopTongueAmp`). No Find.
    // 0 = old cruise shut (swoopGapeAmp 0).
    private float SwoopGape01()
    {
        if (swoopGapeAmp < 0.01f)
            return 0f;
        if (mode != BirdMode.Fly && mode != BirdMode.Glide)
            return 0f;
        if (energyRest)
            return 0f;
        if (OnDirtSupport())
            return 0f;
        if (TakeoffWing01() > 0.01f || TakeoffTrail01() > 0.01f)
            return 0f;
        if (bodyRb != null && bodyRb.linearVelocity.y > -0.08f)
            return 0f;
        if (AlarmLook() > 0.01f)
            return 0f;
        if (flight == null)
            return 0f;
        float t = flight.swoopLook;
        if (t < 0.02f)
            return 0f;
        return Mathf.Clamp(swoopGapeAmp, 0f, 1.5f) * t;
    }

    // Fly/Glide dirt swoop: hang the feet out of the belly-tuck.
    // Picture only — DriveLegs still holds ragdoll pose. Lift stays
    // in BirdFlight. Wing, tail, pupil, lid, and bill already read
    // the dive; Fly/Glide still held tuckHip 50 / tuckKnee 85, so
    // the dirt approach still flashed a cruise fold until flare.
    // Scale with swoopLook (mid-window is strongest), not a pulse.
    // Hang toward flare gear, not Attack toes (stoopTalonAmp) and
    // not the plant (landGearAmp). Skip climb and dirt takeoff
    // (vy and trail). Flare owns landGearAmp. Attack stoop owns
    // stoopTalonAmp. Takeoff trail owns takeoffTrailAmp. Alarm
    // glance skip. Hen skip (hoverMean < 1, swoopLook 0). Tongue
    // is independently gated (`swoopTongueAmp`). No Find.
    // 0 = old cruise tuck (swoopTalonAmp 0).
    private float SwoopTalon01()
    {
        if (swoopTalonAmp < 0.01f)
            return 0f;
        if (mode != BirdMode.Fly && mode != BirdMode.Glide)
            return 0f;
        if (energyRest)
            return 0f;
        if (OnDirtSupport())
            return 0f;
        if (WantsLandingGear())
            return 0f;
        if (TakeoffWing01() > 0.01f || TakeoffTrail01() > 0.01f)
            return 0f;
        if (bodyRb != null && bodyRb.linearVelocity.y > -0.08f)
            return 0f;
        if (AlarmLook() > 0.01f)
            return 0f;
        if (flight == null)
            return 0f;
        float t = flight.swoopLook;
        if (t < 0.02f)
            return 0f;
        return Mathf.Clamp(swoopTalonAmp, 0f, 1.5f) * t;
    }

    // Fly/Glide dirt swoop: show the tongue in the open bill.
    // Picture only — BeakStrike still reads the trigger. Peck
    // grab stays shut. Cry keeps Alarm (the 0.55 s shout). Attack
    // stoop owns stoopTongueAmp. Glance 8° stays glance. Wing,
    // tail, pupil, lid, bill, and feet already read the dive;
    // ApplyCryTongue still skipped Fly/Glide, so the dirt approach
    // still flashed an empty orange box in a live gape. Scale
    // with SwoopGape01 (tongue in a shut bill is invisible), not
    // a pulse. Climb and dirt takeoff skip with the gape. Flare
    // owns landGapeAmp. Takeoff climb owns takeoffGapeAmp. Air
    // turn owns airTurnGapeAmp. Sit ruffle owns landSettleGapeAmp.
    // Alarm glance skip. Hen skip (hoverMean < 1, swoopLook 0).
    // Talon hang is independently gated (`swoopTalonAmp`). Cached
    // tongue on Bird, no Find.
    // 0 = old empty bill (swoopTongueAmp 0).
    private float SwoopTongue01()
    {
        if (swoopTongueAmp < 0.01f)
            return 0f;
        if (energyRest)
            return 0f;
        float g = SwoopGape01();
        if (g < 0.01f)
            return 0f;
        return Mathf.Clamp01(g) * Mathf.Clamp(swoopTongueAmp, 0f, 1.5f);
    }

    // Walk hop-turn: slit the same lid over that dirt drop.
    // Picture only — Walk scan / Peck / Sit-preen / Sit-scratch
    // lids stay their modes. Attack nict stays the pale cover.
    // Sit blink still owns the shut. Wander already wrote
    // SetFacing; legs, neck, tail, wings, and pupil hop, Walk
    // slit stayed skipped, so the about-face still flashed a
    // round stare on the dirt drop. Hold through the plant
    // (same slot as walkLidAmp), not a pulse — sin edges would
    // snap the round pupil twice. Sit hop-turn owns
    // sitTurnLidAmp on the perch. Air turn owns
    // airTurnLidAmp. Stride slit stays Walk. Peck jab
    // stays Peck. Alarm glance skip. No Find.
    // 0 = old round stare (walkTurnLidAmp 0).
    private float WalkTurnLid01()
    {
        if (walkTurnLidAmp < 0.01f || mode != BirdMode.Walk)
            return 0f;
        if (energyRest)
            return 0f;
        if (WalkTurnHop() < 0.01f)
            return 0f;
        if (AlarmLook() > 0.01f)
            return 0f;
        return Mathf.Clamp(walkTurnLidAmp, 0f, 1.5f);
    }

    // Sit hop-turn: slit the same lid over that perch drop.
    // Picture only — Walk / Peck / Sit-preen / Sit-scratch lids
    // stay their modes. Attack nict stays the pale cover. Sit
    // blink still owns the shut. Wander already wrote SetFacing;
    // legs, neck, tail, wings, and pupil hop, Sit look / preen /
    // scratch stayed skipped, so the about-face still flashed a
    // round stare on the perch drop. Hold through the plant
    // (same slot as walkLidAmp), not a pulse — sin edges would
    // snap the round pupil twice. Walk hop-turn stays on the
    // dirt. Air turn owns airTurnLidAmp. Stride slit stays
    // Walk. Peck jab stays Peck. Alarm glance skip. No Find.
    // 0 = old round stare (sitTurnLidAmp 0).
    private float SitTurnLid01()
    {
        if (sitTurnLidAmp < 0.01f || mode != BirdMode.Sit)
            return 0f;
        if (energyRest)
            return 0f;
        if (SitTurnHop() < 0.01f)
            return 0f;
        if (AlarmLook() > 0.01f)
            return 0f;
        return Mathf.Clamp(sitTurnLidAmp, 0f, 1.5f);
    }

    // Sit forage preen: drop the same pupil toward the bill. Picture
    // only — Walk / Peck eyes stay their modes. Attack nict stays the
    // pale cover. Sit blink still owns the shut. Wander already wrote
    // idlePreen; the slit and nibble still left rest-Y, so grooming
    // read as a horizon stare. Pulse with idlePreen (same as the
    // nibble). Night roost, land ruffle, hop-turn, scratch, and
    // Alarm glance skip. No Find. 0 = old rest pupil
    // (sitPreenEyeAmp 0).
    private float SitPreenEye01()
    {
        if (sitPreenEyeAmp < 0.01f || mode != BirdMode.Sit)
            return 0f;
        if (energyRest)
            return 0f;
        if (idlePreen < 0.01f)
            return 0f;
        if (NightRoost() > 0.35f)
            return 0f;
        if (LandSettle01() > 0.01f)
            return 0f;
        if (SitTurnHop() > 0.01f)
            return 0f;
        if (idleHop > 0.01f)
            return 0f;
        if (AlarmLookSit01() > 0.01f)
            return 0f;
        return idlePreen * Mathf.Clamp(sitPreenEyeAmp, 0f, 1.5f);
    }

    // Sit forage scratch: drop the same pupil toward the kicking
    // foot. Picture only — Walk / Peck / Sit-preen eyes stay their
    // modes. Attack nict stays the pale cover. Sit blink still owns
    // the shut. Wander already wrote idleHop; the slit still left
    // rest-Y, so scratch read as a horizon kick. Pulse with idleHop
    // (same as the lid). Night roost, land ruffle, hop-turn, and
    // Alarm glance skip. Lid rides the child. No Find. 0 = old
    // rest pupil (sitScratchEyeAmp 0).
    private float SitScratchEye01()
    {
        if (sitScratchEyeAmp < 0.01f || sitScratchAmp < 0.01f
            || mode != BirdMode.Sit)
            return 0f;
        if (energyRest)
            return 0f;
        if (idleHop < 0.01f)
            return 0f;
        if (NightRoost() > 0.35f)
            return 0f;
        if (LandSettle01() > 0.01f)
            return 0f;
        if (SitTurnHop() > 0.01f)
            return 0f;
        if (AlarmLookSit01() > 0.01f)
            return 0f;
        return idleHop * Mathf.Clamp(sitScratchEyeAmp, 0f, 1.5f);
    }

    // Sit forage look-around: glance the same pupil with the neck.
    // Picture only — Walk / Peck / Sit-preen / Sit-scratch eyes stay
    // their modes. Attack nict stays the pale cover. Sit blink still
    // owns the shut. Lid stays open (Walk slit is dirt forage).
    // Gape rides look-down (`sitLookGapeAmp`). Wattle hang rides
    // the same look-down (`sitLookWattleAmp`). Comb nod rides
    // the same look-down (`sitLookCombAmp`).
    // Wander already wrote idleLook; the neck weaved, rest-Y stayed
    // socket-forward, so look-around read as a nodding statue eye.
    // Signed with idleLook: down toward the bill, up toward the nape.
    // Night roost, land ruffle, hop-turn, preen, and scratch skip.
    // Sit ruffle owns landSettleEyeAmp on that skip. Sit ruffle
    // lid owns landSettleLidAmp. Lid rides
    // the child. No Find. 0 = old rest pupil
    // (sitLookEyeAmp 0).
    private float SitLookEye01()
    {
        if (sitLookEyeAmp < 0.01f || mode != BirdMode.Sit)
            return 0f;
        if (energyRest)
            return 0f;
        if (Mathf.Abs(idleLook) < 0.01f)
            return 0f;
        if (NightRoost() > 0.35f)
            return 0f;
        if (LandSettle01() > 0.01f)
            return 0f;
        if (SitTurnHop() > 0.01f)
            return 0f;
        if (idlePreen > 0.01f)
            return 0f;
        if (idleHop > 0.01f)
            return 0f;
        if (AlarmLook() > 0.01f)
            return 0f;
        return idleLook * Mathf.Clamp(sitLookEyeAmp, 0f, 1.5f);
    }

    // Sit forage look-around: counter-fan the tail with the neck.
    // Picture only — DriveTail still holds ragdoll pitch. Scratch
    // owns sitScratchTailAmp. Hop-turn owns sitTurnTailAmp. Sit
    // ruffle owns landSettleTailAmp. Pad
    // crouch / Alarm keep their own fans. Walk stride and hen hop
    // stay Walk. Wander
    // already wrote idleLook; the neck and pupil weaved, Sit drop
    // 6° stayed put, so look-around still read as a nodding statue
    // perch. Signed opposite the neck: head toward the bill streams
    // the free end, toward the nape drops it. Night roost, land
    // ruffle, hop-turn, preen, and scratch skip. No Find.
    // 0 = old Sit drop (sitLookTailAmp 0).
    private float SitLookTail01()
    {
        if (sitLookTailAmp < 0.01f || mode != BirdMode.Sit)
            return 0f;
        if (energyRest)
            return 0f;
        if (Mathf.Abs(idleLook) < 0.01f)
            return 0f;
        if (NightRoost() > 0.35f)
            return 0f;
        if (LandSettle01() > 0.01f)
            return 0f;
        if (SitTurnHop() > 0.01f)
            return 0f;
        if (idlePreen > 0.01f)
            return 0f;
        if (idleHop > 0.01f)
            return 0f;
        if (AlarmLook() > 0.01f)
            return 0f;
        return idleLook * Mathf.Clamp(sitLookTailAmp, 0f, 1.5f);
    }

    // Sit forage preen: drop the tail with the reach. Picture only —
    // DriveTail still holds ragdoll pitch. Look owns sitLookTailAmp.
    // Scratch owns sitScratchTailAmp. Hop-turn owns sitTurnTailAmp.
    // Sit ruffle owns landSettleTailAmp. Pad crouch / Alarm keep
    // their own fans. Walk stride and hen
    // hop stay Walk. Wander
    // already wrote idlePreen; the wing, slit, nibble, and pupil
    // were live, Sit drop 6° stayed put, so grooming still read as
    // a stretching statue perch. Pulse with idlePreen (same as the
    // nibble). Night roost, land ruffle, hop-turn, look, and
    // scratch skip. No Find. 0 = old Sit drop (sitPreenTailAmp 0).
    private float SitPreenTail01()
    {
        if (sitPreenTailAmp < 0.01f || mode != BirdMode.Sit)
            return 0f;
        if (energyRest)
            return 0f;
        if (idlePreen < 0.01f)
            return 0f;
        if (NightRoost() > 0.35f)
            return 0f;
        if (LandSettle01() > 0.01f)
            return 0f;
        if (SitTurnHop() > 0.01f)
            return 0f;
        if (Mathf.Abs(idleLook) > 0.01f)
            return 0f;
        if (idleHop > 0.01f)
            return 0f;
        if (AlarmLook() > 0.01f)
            return 0f;
        return idlePreen * Mathf.Clamp(sitPreenTailAmp, 0f, 1.5f);
    }

    // Sit forage look-around: hitch the far (left) wing opposite the
    // neck. Picture only — DriveWings still holds ragdoll pose. Lift
    // stays in BirdFlight. Wander already wrote idleLook; the neck,
    // pupil, and tail weaved, Sit fold stayed put, so look-around
    // still read as a nodding statue with a wagging tail. Signed
    // opposite the neck: bill-down lifts the box, nape-up presses
    // the tuck. Scratch owns the kick flick. Preen owns the near
    // wing and its far-wing brace. Hop-turn / pad hunch / Alarm
    // keep their own folds. Night roost, land ruffle, hop-turn,
    // preen, and scratch skip. No Find. 0 = old Sit fold
    // (sitLookWingAmp 0).
    private float SitLookWing01()
    {
        if (sitLookWingAmp < 0.01f || mode != BirdMode.Sit)
            return 0f;
        if (energyRest)
            return 0f;
        if (Mathf.Abs(idleLook) < 0.01f)
            return 0f;
        if (NightRoost() > 0.35f)
            return 0f;
        if (LandSettle01() > 0.01f)
            return 0f;
        if (SitTurnHop() > 0.01f)
            return 0f;
        if (idlePreen > 0.01f)
            return 0f;
        if (idleHop > 0.01f)
            return 0f;
        if (AlarmLook() > 0.01f)
            return 0f;
        return idleLook * Mathf.Clamp(sitLookWingAmp, 0f, 1.5f);
    }

    // Sit forage preen: hitch the far (left) wing while the near
    // reaches. Picture only — DriveWings still holds ragdoll pose.
    // Lift stays in BirdFlight. Wander already wrote idlePreen; the
    // near wing, slit, nibble, pupil, and tail were live, Sit fold
    // stayed put, so grooming still read as a stretching statue
    // with one wing posed. Plus from −90 (brace), not more-negative
    // (near already to the beak). Look owns the weave hitch.
    // Scratch owns the kick flick. Hop-turn / pad hunch / Alarm
    // keep their own folds. Night roost, land ruffle, hop-turn,
    // look, and scratch skip. No Find. 0 = old Sit fold
    // (sitPreenWingAmp 0).
    private float SitPreenWing01()
    {
        if (sitPreenWingAmp < 0.01f || mode != BirdMode.Sit)
            return 0f;
        if (energyRest)
            return 0f;
        if (idlePreen < 0.01f)
            return 0f;
        if (NightRoost() > 0.35f)
            return 0f;
        if (LandSettle01() > 0.01f)
            return 0f;
        if (SitTurnHop() > 0.01f)
            return 0f;
        if (Mathf.Abs(idleLook) > 0.01f)
            return 0f;
        if (idleHop > 0.01f)
            return 0f;
        if (AlarmLook() > 0.01f)
            return 0f;
        return idlePreen * Mathf.Clamp(sitPreenWingAmp, 0f, 1.5f);
    }

    // 0 roost / ruffle / hop-turn / look / scratch / off, pulse
    // with idlePreen. Includes sitPreenWingAmp.
    private void ApplySitPreenWingPicture()
    {
        float u = SitPreenWing01();
        if (u < 0.01f)
            return;
        // Plus from −90 lifts the box (hop shrug), not preen
        // (more-negative, near already at the beak). Pulse with
        // idlePreen so the brace is the reach, not a second lift.
        flockLeftWingFold += 16f * u;
        // B-WING-6: the same lift out of the fold (was the -16 pair).
        flockLeftUlnaZ = Mathf.Lerp(flockLeftUlnaZ, groundFoldUlnaZ + groundFlickLiftDeg, Mathf.Clamp01(u));
    }

    // 0 roost / ruffle / hop-turn / preen / scratch / off, signed
    // with idleLook. Includes sitLookWingAmp.
    private void ApplySitLookWingPicture()
    {
        float w = SitLookWing01();
        if (Mathf.Abs(w) < 0.01f)
            return;
        // Plus from −90 lifts the box (hop shrug), not preen
        // (more-negative, near wing to the beak). Opposite the
        // neck so the weave is a counter, not a second nod.
        float hitch = -w;
        flockLeftWingFold += 16f * hitch;
        if (hitch > 0.01f)
            flockLeftUlnaZ = Mathf.Lerp(flockLeftUlnaZ, groundFoldUlnaZ + groundFlickLiftDeg, Mathf.Clamp01(hitch));
    }

    // Hen/crow Peck: hold the same dirt drop through jab and swallow.
    // Picture only — Walk eye stays Walk. Attack nict stays the pale
    // cover. Lid still slits on the down-stroke; swallow opens, so
    // rest-Y made the toss a commute stare. Held, not Cos. Woodpecker
    // drum stays the trunk stare. No Find. 0 = old rest pupil
    // (peckEyeAmp 0).
    private float PeckScanEye01()
    {
        if (peckEyeAmp < 0.01f || mode != BirdMode.Peck)
            return 0f;
        if (energyRest)
            return 0f;
        if (IsWoodpeckerDrum())
            return 0f;
        if (sensors != null && !sensors.bothGrounded)
            return 0f;
        if (AlarmLook() > 0.01f)
            return 0f;
        return Mathf.Clamp(peckEyeAmp, 0f, 1.5f);
    }

    // Dirt Walk forage: slit a lid over the dropped pupil. Picture
    // only — Sit blink still owns the shut. The drop left a round
    // stare on the dirt, so forage still read as a commute with a
    // lowered eye. Child of the pupil; Attack nict stays the pale
    // cover. Peck jab reuses this lid on the down-stroke. Sit
    // idlePreen reuses it on the reach (`sitPreenLidAmp`). Sit
    // idleHop scratch reuses it on the kick (`sitScratchLidAmp`).
    // Walk hop-turn reuses it on the plant (`walkTurnLidAmp`).
    // Sit hop-turn reuses it on the perch (`sitTurnLidAmp`).
    // Fly/Glide air turn reuses it on the bank (`airTurnLidAmp`).
    // Landing flare reuses it on the plant (`landLidAmp`).
    // Dirt swoop reuses it on the dive (`swoopLidAmp`).
    // Takeoff climb reuses it on the hop (`takeoffLidAmp`).
    // Sit land-ruffle reuses it on the plant (`landSettleLidAmp`).
    // Alarm glance reuses it on Flee (`alarmLookLidAmp`).
    // Cached SpriteRenderer on Bird, no Find.
    // 0 = old round stare (walkLidAmp 0) / open-eye jab
    // (peckLidAmp 0) / open-eye preen (sitPreenLidAmp 0) /
    // open-eye scratch (sitScratchLidAmp 0) / open-eye hop-turn
    // (walkTurnLidAmp 0) / open-eye Sit hop-turn (sitTurnLidAmp 0)
    // / open-eye air turn (airTurnLidAmp 0) / open-eye land
    // flare (landLidAmp 0) / open-eye dirt swoop
    // (swoopLidAmp 0) / open-eye takeoff climb
    // (takeoffLidAmp 0) / open-eye Sit land-ruffle
    // (landSettleLidAmp 0) / open-eye Alarm glance
    // (alarmLookLidAmp 0).
    private void ApplyWalkScanLid()
    {
        if (bird == null || bird.lidRenderer == null)
            return;
        SpriteRenderer lid = bird.lidRenderer;
        bool on = !energyRest
            && lastEyeOn
            && (WalkScanLid01() > 0.01f || PeckLid01() > 0.01f
                || SitPreenLid01() > 0.01f || SitScratchLid01() > 0.01f
                || WalkTurnLid01() > 0.01f || SitTurnLid01() > 0.01f
                || AirTurnLid01() > 0.01f || LandLid01() > 0.01f
                || SwoopLid01() > 0.01f || TakeoffLid01() > 0.01f
                || LandSettleLid01() > 0.01f || AlarmLookLid01() > 0.01f);
        if (on == lastLidOn && lid.enabled == on)
            return;
        lid.enabled = on;
        lastLidOn = on;
    }

    private float WalkScanLid01()
    {
        if (walkLidAmp < 0.01f || mode != BirdMode.Walk)
            return 0f;
        if (energyRest)
            return 0f;
        if (sensors != null && !sensors.bothGrounded)
            return 0f;
        if (WalkTurnHop() > 0.01f)
            return 0f;
        if (AlarmLook() > 0.01f)
            return 0f;
        return Mathf.Clamp(walkLidAmp, 0f, 1.5f);
    }

    // Sit forage preen: slit the same lid on the reach. Picture only —
    // Walk / Peck lids stay their modes. Attack nict stays the pale
    // cover. Sit blink still owns the shut. Wander already wrote
    // idlePreen; the round stare made grooming a stretch. Night roost,
    // land ruffle, hop-turn, scratch, and Alarm glance skip. No Find.
    // 0 = old round stare (sitPreenLidAmp 0).
    private float SitPreenLid01()
    {
        if (sitPreenLidAmp < 0.01f || mode != BirdMode.Sit)
            return 0f;
        if (energyRest)
            return 0f;
        if (idlePreen < 0.01f)
            return 0f;
        if (NightRoost() > 0.35f)
            return 0f;
        if (LandSettle01() > 0.01f)
            return 0f;
        if (SitTurnHop() > 0.01f)
            return 0f;
        if (idleHop > 0.01f)
            return 0f;
        if (AlarmLookSit01() > 0.01f)
            return 0f;
        return Mathf.Clamp(sitPreenLidAmp, 0f, 1.5f);
    }

    // Sit forage scratch: slit the same lid on the kick. Picture only —
    // Walk / Peck / Sit-preen lids stay their modes. Attack nict stays
    // the pale cover. Sit blink still owns the shut. Wander already
    // wrote idleHop; the foot reached the head, so the round stare
    // made scratch a spasm. Night roost, land ruffle, hop-turn, and
    // Alarm glance skip. No Find. 0 = old round stare (sitScratchLidAmp 0).
    private float SitScratchLid01()
    {
        if (sitScratchLidAmp < 0.01f || sitScratchAmp < 0.01f
            || mode != BirdMode.Sit)
            return 0f;
        if (energyRest)
            return 0f;
        if (idleHop < 0.01f)
            return 0f;
        if (NightRoost() > 0.35f)
            return 0f;
        if (LandSettle01() > 0.01f)
            return 0f;
        if (SitTurnHop() > 0.01f)
            return 0f;
        if (AlarmLookSit01() > 0.01f)
            return 0f;
        return idleHop * Mathf.Clamp(sitScratchLidAmp, 0f, 1.5f);
    }

    // Hen/crow Peck: slit the same lid on the jab. Picture only —
    // Walk lid stays Walk. Attack nict stays the pale cover. Sit
    // blink still owns the shut. Pupil drop holds through swallow
    // (`peckEyeAmp`). Same Cos as gape (down), zero on the toss.
    // Woodpecker drum stays the trunk stare. No Find.
    // 0 = old open pupil (peckLidAmp 0).
    private float PeckLid01()
    {
        if (peckLidAmp < 0.01f || mode != BirdMode.Peck)
            return 0f;
        if (energyRest)
            return 0f;
        if (IsWoodpeckerDrum())
            return 0f;
        if (sensors != null && !sensors.bothGrounded)
            return 0f;
        if (AlarmLook() > 0.01f)
            return 0f;
        return PeckDown01() * Mathf.Clamp(peckLidAmp, 0f, 1.5f);
    }

    // Hen/crow Peck: bulge the gullet on the swallow toss. Picture
    // only — Walk gape stays Walk. Attack stoop keeps its own gape.
    // Pupil drop holds through swallow (`peckEyeAmp`); lid still
    // opens on the toss, so the silent neck made eat a nod with
    // nowhere for the bite. Same Cos as swallow (up), zero on the
    // jab. Woodpecker drum stays the brace. Alarm/Call shout
    // reuses the same sprite (`cryCropAmp`) — gape and neck lift
    // were live, the crop stayed off, so Flee still flashed a mute
    // open-bill. Scale with CryGape, not the 1.15 s glance. Peck
    // swallow wins if both. Attack stoop reuses the same sprite
    // (`stoopCropAmp`) — gape, nict, tongue, and wattle were live,
    // the crop stayed off, so the dive still flashed a mute open-bill.
    // Scale with StoopGape. Cry keeps Alarm. Comb stays glued
    // (contrast). Shout tongue is independently gated
    // (`cryTongueAmp`). Attack stoop tongue is independently gated
    // (`stoopTongueAmp`). Shout hen wattle is independently gated
    // (`cryWattleAmp`). Attack stoop hen wattle is independently
    // gated (`stoopWattleAmp`). Shout hen comb is independently gated
    // (`cryCombAmp`). Peck swallow hen wattle is independently
    // gated (`peckWattleAmp`); without it the toss still left a
    // glued throat over a live crop. Peck swallow hen comb is
    // independently gated (`peckCombAmp`); without it the toss
    // still left a glued crown over a live throat. Cached
    // SpriteRenderer on Bird, no Find.
    // 0 = old silent neck (peckCropAmp 0 / cryCropAmp 0 / stoopCropAmp 0).
    private void ApplyPeckCrop()
    {
        if (bird == null || bird.cropRenderer == null)
            return;
        SpriteRenderer crop = bird.cropRenderer;
        float a = PeckCrop01();
        if (a < 0.04f)
            a = CryCrop01();
        if (a < 0.04f)
            a = StoopCrop01();
        bool on = a > 0.04f;
        if (!on)
        {
            if (!lastCropOn)
                return;
            crop.enabled = false;
            lastCropOn = false;
            lastCropAmt = float.NaN;
            return;
        }

        if (lastCropOn && Mathf.Abs(a - lastCropAmt) < 0.01f)
            return;
        lastCropOn = true;
        lastCropAmt = a;
        crop.enabled = true;
        Vector2 rest = bird.cropRestSize;
        crop.size = new Vector2(rest.x * (1f + 0.85f * a), rest.y * (1f + 1.15f * a));
        Vector3 pos = bird.cropRestLocal;
        pos.x += rest.x * 0.35f * a;
        pos.y -= rest.y * 0.20f * a;
        crop.transform.localPosition = pos;
        Color c = crop.color;
        c.a = 0.55f + 0.30f * a;
        crop.color = c;
    }

    private float PeckCrop01()
    {
        if (peckCropAmp < 0.01f || mode != BirdMode.Peck)
            return 0f;
        if (energyRest)
            return 0f;
        float u = PeckSwallow01();
        if (u < 0.01f)
            return 0f;
        return u * Mathf.Clamp(peckCropAmp, 0f, 1.5f);
    }

    // BIRD-6 Alarm/Call: bulge the gullet with the 0.55 s shout.
    // Picture only — Peck swallow keeps peckCropAmp. Glance gape /
    // neck / wing stay theirs. Gape and neck already open; without
    // this Flee still flashed a mute open-bill. Scale with CryGape,
    // not AlarmLook. Woodpecker drum stays the brace. Attack stoop
    // reuses the same sprite (`stoopCropAmp`). Cached crop
    // on Bird, no Find. 0 = old silent neck (cryCropAmp 0).
    // Shout tongue is independently gated (`cryTongueAmp`).
    // Attack stoop crop is independently gated (`stoopCropAmp`).
    // Attack stoop tongue is independently gated (`stoopTongueAmp`).
    // Shout hen wattle is independently gated (`cryWattleAmp`).
    // Attack stoop hen wattle is independently gated (`stoopWattleAmp`).
    // Shout hen comb is independently gated (`cryCombAmp`).
    private float CryCrop01()
    {
        if (cryCropAmp < 0.01f)
            return 0f;
        if (energyRest)
            return 0f;
        if (mode == BirdMode.Peck || mode == BirdMode.Attack)
            return 0f;
        float g = CryGape();
        if (g < 0.01f)
            return 0f;
        return g * Mathf.Clamp(cryCropAmp, 0f, 1.5f);
    }

    // Attack stoop: bulge the gullet in the dive. Picture only —
    // BeakStrike still reads the trigger. Peck swallow keeps
    // peckCropAmp. Cry keeps Alarm (the 0.55 s shout). Glance 8°
    // stays glance. Comb stays glued (contrast). Gape, nict,
    // tongue, and wattle already open; without this the dive still
    // flashed a mute open-bill. Scale with StoopGape. Cached crop
    // on Bird, no Find. 0 = old silent neck (stoopCropAmp 0).
    private float StoopCrop01()
    {
        if (stoopCropAmp < 0.01f)
            return 0f;
        if (energyRest)
            return 0f;
        if (mode != BirdMode.Attack)
            return 0f;
        float g = StoopGape();
        if (g < 0.01f)
            return 0f;
        return Mathf.Clamp01(g) * Mathf.Clamp(stoopCropAmp, 0f, 1.5f);
    }

    // BIRD-6 Alarm/Call: show the tongue in the 0.55 s gape.
    // Picture only — Peck grab stays shut-tongue. Glance 8° stays
    // glance. Crop already bulged; without this Flee still flashed
    // an empty orange bill. Scale with CryGape, not AlarmLook.
    // Attack stoop reuses the same sprite (`stoopTongueAmp`) —
    // gape and nict were live, the orange box stayed empty, so the
    // dive still flashed a mute gape. Scale with StoopGape. Dirt
    // swoop reuses it (`swoopTongueAmp`) — gape already parts on
    // the dive, the orange box stayed empty, so the dirt approach
    // still flashed a mute gape. Scale with SwoopGape01. Cry
    // wins if both; Attack stoop wins over swoop. Cached tongue
    // on Bird, no Find.
    // Attack stoop hen wattle is independently gated (`stoopWattleAmp`).
    // 0 = old empty bill (cryTongueAmp 0 / stoopTongueAmp 0 /
    // swoopTongueAmp 0).
    private void ApplyCryTongue()
    {
        if (bird == null || bird.tongueRenderer == null)
            return;
        SpriteRenderer tongue = bird.tongueRenderer;
        float a = CryTongue01();
        if (a < 0.04f)
            a = StoopTongue01();
        if (a < 0.04f)
            a = SwoopTongue01();
        bool on = a > 0.04f;
        if (!on)
        {
            if (!lastTongueOn)
                return;
            tongue.enabled = false;
            lastTongueOn = false;
            lastTongueAmt = float.NaN;
            return;
        }

        if (lastTongueOn && Mathf.Abs(a - lastTongueAmt) < 0.01f)
            return;
        lastTongueOn = true;
        lastTongueAmt = a;
        tongue.enabled = true;
        Vector2 rest = bird.tongueRestSize;
        tongue.size = new Vector2(rest.x * (1f + 1.20f * a), rest.y * (1f + 0.55f * a));
        Vector3 pos = bird.tongueRestLocal;
        pos.x += rest.x * 0.55f * a;
        pos.y -= rest.y * 0.80f * a;
        tongue.transform.localPosition = pos;
        Color c = tongue.color;
        c.a = 0.70f + 0.25f * a;
        tongue.color = c;
    }

    private float CryTongue01()
    {
        if (cryTongueAmp < 0.01f)
            return 0f;
        if (energyRest)
            return 0f;
        if (mode == BirdMode.Peck || mode == BirdMode.Attack)
            return 0f;
        float g = CryGape();
        if (g < 0.01f)
            return 0f;
        return g * Mathf.Clamp(cryTongueAmp, 0f, 1.5f);
    }

    // Attack stoop: show the tongue in the open bill. Picture only —
    // BeakStrike still reads the trigger. Peck grab stays shut.
    // Cry keeps Alarm (the 0.55 s shout). Glance 8° stays glance.
    // Gape and nict already open; without this the dive still
    // flashed an empty orange bill. Scale with StoopGape. Cached
    // tongue on Bird, no Find. 0 = old empty bill (stoopTongueAmp 0).
    private float StoopTongue01()
    {
        if (stoopTongueAmp < 0.01f)
            return 0f;
        if (energyRest)
            return 0f;
        if (mode != BirdMode.Attack)
            return 0f;
        float g = StoopGape();
        if (g < 0.01f)
            return 0f;
        return Mathf.Clamp01(g) * Mathf.Clamp(stoopTongueAmp, 0f, 1.5f);
    }

    // BIRD-6 Alarm/Call: swing the hen wattle with the 0.55 s shout.
    // Picture only — comb nod is independently gated (`cryCombAmp`).
    // Crow / woodpecker have no mark. Peck grab already rides the
    // head dip. Attack stoop reuses the same sprite (`stoopWattleAmp`)
    // — gape, nict, and tongue were live, the throat mark stayed
    // glued to Head Visual, so the dive still flashed a statue
    // wattle. Scale with StoopGape. Hen Walk hop reuses it
    // (`walkWattleAmp`) — legs, wings, tail, eye, lid, and bill
    // already hop, the throat mark stayed glued, so Chicken Walk
    // still flashed a statue wattle. Same WalkHopDown01 tuck.
    // Sit idleLook reuses it (`sitLookWattleAmp`) — neck, pupil,
    // tail, wing, and bill already weave, the throat mark stayed
    // glued, so Chicken Sit still flashed a statue wattle on
    // look-down. Same SitLookGape01 clock (toward the bill).
    // Sit idlePreen reuses it (`sitPreenWattleAmp`) — wing, slit,
    // nibble, pupil, tail, and far-wing brace already reach, the
    // throat mark stayed glued, so Chicken Sit still flashed a
    // statue wattle on the nibble. Same SitPreenGape01 clock.
    // Sit idleHop reuses it (`sitScratchWattleAmp`) — foot, slit,
    // pupil, wing, bill, and tail already kick, the throat mark
    // stayed glued, so Chicken Sit still flashed a statue wattle
    // on the kick. Same SitScratchGape01 clock.
    // Sit hop-turn reuses it (`sitTurnWattleAmp`) — legs, neck,
    // tail, wings, pupil, lid, and bill already hop, Sit look /
    // preen / scratch wattle stayed skipped, so the about-face
    // still flashed a statue wattle on the perch drop. Same
    // SitTurnGape01 hold.
    // Sit land-ruffle reuses it (`landSettleWattleAmp`) — wings,
    // neck, pupil, lid, bill, and tail already shake, Sit look /
    // preen / scratch / hop-turn wattle stayed skipped, so the
    // plant still flashed a statue wattle. Same LandSettleGape01
    // plant.
    // Peck swallow reuses it (`peckWattleAmp`) — crop already
    // bulges on the toss, the throat mark stayed glued to Head
    // Visual, so Chicken Peck still flashed a statue wattle on
    // the swallow. Same PeckSwallow01 toss. Peck grab still rides
    // the head dip. Comb nod is independently gated
    // (`peckCombAmp`). Woodpecker drum skip.
    // Walk hop-turn reuses it (`walkTurnWattleAmp`) — legs, neck,
    // tail, wings, pupil, lid, and bill already hop, Walk hop
    // wattle stayed skipped, so the about-face still flashed a
    // statue wattle on the dirt drop. Same WalkTurnGape01 hold.
    // Fly first-air reuses it (`takeoffWattleAmp`) — legs, wings,
    // tail, pupil, lid, and bill already climb, the throat mark
    // stayed glued to Head Visual, so Chicken Fly still flashed
    // a statue wattle on the hop. Same first-air clock as
    // TakeoffGape01 (leave is strongest). Dirt coil skip. Sinking
    // skip (flare owns landWattleAmp; swoop wattle stays glued).
    // Comb nod is independently gated (`takeoffCombAmp`).
    // Fly/Glide flare reuses it (`landWattleAmp`) — gear, cup,
    // tail, pupil, lid, and bill already drop, the throat mark
    // stayed glued to Head Visual, so Chicken Fly/Glide still
    // flashed a statue wattle on the plant. Same flareLook as
    // LandGape01 (closer is stronger). Climb and dirt takeoff
    // skip. Comb nod is independently gated (`landCombAmp`).
    // Swoop wattle stays glued.
    // Cry wins, then stoop, then Alarm glance, then Walk hop,
    // then Walk hop-turn, then Sit look, then Sit preen, then Sit
    // scratch, then Sit hop-turn, then Sit land-ruffle, then Peck
    // swallow, then takeoff climb, then land-flare.
    // Alarm glance reuses it (`alarmLookWattleAmp`) — neck, tail,
    // pupil, lid, bill, and wings already glance after the 0.55 s
    // shout; the throat mark stayed glued to Head Visual, so
    // Chicken Flee still flashed a statue wattle on the 1.15 s
    // look-back. Same AlarmLook as AlarmLookGape01. Cry shout
    // stays Cry (listed first). Comb nod is independently gated
    // (`alarmLookCombAmp`). Swoop wattle stays glued (hens
    // skip swoop; crow unmarked). Glance 8° stays the neck.
    // Comb nod is independently gated (`cryCombAmp`
    // shout, `alarmLookCombAmp` glance, `walkCombAmp` hop,
    // `walkTurnCombAmp` dirt hop-turn,
    // `sitTurnCombAmp` perch hop-turn, `sitLookCombAmp` look-down,
    // `sitPreenCombAmp` nibble, `sitScratchCombAmp` kick,
    // `landSettleCombAmp` plant, `peckCombAmp` toss,
    // `takeoffCombAmp` climb, `landCombAmp` plant). Peck grab
    // still rides the head dip. Stoop stays glued (contrast).
    // Swoop wattle stays glued (air window). Cached
    // wattle on Bird, no Find. 0 = old
    // statue wattle (cryWattleAmp 0 / stoopWattleAmp 0 /
    // alarmLookWattleAmp 0 / walkWattleAmp 0 / walkTurnWattleAmp 0 /
    // sitLookWattleAmp 0 /
    // sitPreenWattleAmp 0 / sitScratchWattleAmp 0 /
    // sitTurnWattleAmp 0 / landSettleWattleAmp 0 /
    // peckWattleAmp 0 / takeoffWattleAmp 0 /
    // landWattleAmp 0).
    private void ApplyCryWattle()
    {
        if (bird == null || bird.wattleRenderer == null)
            return;
        SpriteRenderer wattle = bird.wattleRenderer;
        float a = CryWattle01();
        if (a < 0.04f)
            a = StoopWattle01();
        if (a < 0.04f)
            a = AlarmLookWattle01();
        if (a < 0.04f)
            a = WalkWattle01();
        if (a < 0.04f)
            a = WalkTurnWattle01();
        if (a < 0.04f)
            a = SitLookWattle01();
        if (a < 0.04f)
            a = SitPreenWattle01();
        if (a < 0.04f)
            a = SitScratchWattle01();
        if (a < 0.04f)
            a = SitTurnWattle01();
        if (a < 0.04f)
            a = LandSettleWattle01();
        if (a < 0.04f)
            a = PeckWattle01();
        if (a < 0.04f)
            a = TakeoffWattle01();
        if (a < 0.04f)
            a = LandWattle01();
        bool on = a > 0.04f;
        if (!on)
        {
            if (!lastWattleOn)
                return;
            wattle.size = bird.wattleRestSize;
            wattle.transform.localPosition = bird.wattleRestLocal;
            wattle.transform.localRotation = Quaternion.identity;
            lastWattleOn = false;
            lastWattleAmt = float.NaN;
            return;
        }

        if (lastWattleOn && Mathf.Abs(a - lastWattleAmt) < 0.01f)
            return;
        lastWattleOn = true;
        lastWattleAmt = a;
        Vector2 rest = bird.wattleRestSize;
        wattle.size = new Vector2(rest.x * (1f + 0.28f * a), rest.y * (1f + 0.95f * a));
        Vector3 pos = bird.wattleRestLocal;
        pos.x += rest.x * 0.22f * a;
        pos.y -= rest.y * 0.70f * a;
        wattle.transform.localPosition = pos;
        wattle.transform.localRotation = Quaternion.Euler(0f, 0f, -22f * a);
    }

    private float CryWattle01()
    {
        if (cryWattleAmp < 0.01f)
            return 0f;
        if (energyRest)
            return 0f;
        if (mode == BirdMode.Peck || mode == BirdMode.Attack)
            return 0f;
        float g = CryGape();
        if (g < 0.01f)
            return 0f;
        return g * Mathf.Clamp(cryWattleAmp, 0f, 1.5f);
    }

    // Attack stoop: swing the hen wattle in the dive. Picture only —
    // BeakStrike still reads the trigger. Peck grab already rides
    // the head dip. Cry keeps Alarm (the 0.55 s shout). Glance 8°
    // stays glance. Comb stays glued (contrast — shout nod is
    // cryCombAmp). Gape, nict, and tongue already open; without
    // this the Chicken dive still flashed a statue throat. Scale
    // with StoopGape. Cached wattle on Bird, no Find. 0 = old
    // glued wattle (stoopWattleAmp 0).
    private float StoopWattle01()
    {
        if (stoopWattleAmp < 0.01f)
            return 0f;
        if (energyRest)
            return 0f;
        if (mode != BirdMode.Attack)
            return 0f;
        float g = StoopGape();
        if (g < 0.01f)
            return 0f;
        return Mathf.Clamp01(g) * Mathf.Clamp(stoopWattleAmp, 0f, 1.5f);
    }

    // BIRD-6 Flee: hang the hen wattle through the glance after
    // cry. Picture only — Cry keeps Alarm (the 0.55 s shout,
    // listed first). Attack stoop stays Attack. Walk hop / Walk
    // hop-turn / Sit look / preen / scratch / hop-turn /
    // land-ruffle keep their hang (those skip AlarmLook). Peck
    // grab already rides the head dip. Comb nod is independently
    // gated (`alarmLookCombAmp`). Crow unmarked. Neck, tail,
    // pupil, lid, bill, and wings already glance; the throat mark
    // stayed glued to Head Visual, so Chicken Flee still flashed
    // a statue wattle on the 1.15 s look-back after the shout
    // faded. Hang with AlarmLook (same envelope as
    // AlarmLookGape01), not a shout. Night roost skip. Swoop
    // wattle stays glued (hens skip swoop; crow unmarked). Cached
    // wattle on Bird, no Find.
    // 0 = old glued glance wattle (alarmLookWattleAmp 0).
    private float AlarmLookWattle01()
    {
        if (alarmLookWattleAmp < 0.01f)
            return 0f;
        if (energyRest)
            return 0f;
        if (NightRoost() > 0.35f)
            return 0f;
        float look = AlarmLook();
        if (look < 0.02f)
            return 0f;
        float a = Mathf.Max(0.01f, Mathf.Clamp(alarmLookAmp, 0f, 1.5f));
        return Mathf.Clamp(alarmLookWattleAmp, 0f, 1.5f) * (look / a);
    }

    // Hen Walk hop: swing the wattle on the tuck. Picture only —
    // walkThrust still translates. Cry keeps Alarm. Attack stoop
    // stays Attack. Peck grab already rides the head dip. Comb
    // nod is independently gated (`walkCombAmp`). Crow stride
    // has no mark (`walkHopAmp` 0). Alarm glance hang is
    // independently gated (`alarmLookWattleAmp`); this skip
    // AlarmLook so the look-back owns the throat. Legs, wings, tail, eye, lid, and bill
    // already hop; without this the Chicken commute still flashed
    // a statue throat. Hang with WalkHopDown01 (tuck = hang).
    // Walk hop-turn hang is independently gated
    // (`walkTurnWattleAmp`) — this skip WalkTurnHop so the
    // about-face owns the throat. Cached wattle on Bird, no Find.
    // 0 = old glued Walk wattle (walkWattleAmp 0).
    private float WalkWattle01()
    {
        if (walkWattleAmp < 0.01f)
            return 0f;
        if (energyRest)
            return 0f;
        if (mode != BirdMode.Walk)
            return 0f;
        if (walkHopAmp < 0.01f)
            return 0f;
        if (WalkTurnHop() > 0.01f)
            return 0f;
        if (AlarmLook() > 0.01f)
            return 0f;
        float hang = 1f - WalkHopDown01();
        if (hang < 0.04f)
            return 0f;
        return hang
            * Mathf.Clamp(walkWattleAmp, 0f, 1.5f)
            * Mathf.Clamp(walkHopAmp, 0f, 1.5f);
    }

    // Sit forage look-around: hang the hen wattle on look-down.
    // Picture only — Cry keeps Alarm. Attack stoop stays Attack.
    // Walk hop stays Walk. Peck grab already rides the head dip.
    // Comb nod is independently gated (`sitLookCombAmp`). Crow
    // unmarked. Hop-turn hang
    // is independently gated (`sitTurnWattleAmp`). Preen nibble
    // is independently gated (`sitPreenWattleAmp`). Scratch hang
    // is independently gated (`sitScratchWattleAmp`). Preen nod
    // is independently gated (`sitPreenCombAmp`). Wander
    // already wrote idleLook; neck,
    // pupil, tail, wing, and bill weave, Sit look / preen /
    // scratch wattle stayed skipped, so look-down still flashed a
    // statue throat. Hang with SitLookGape01 (toward the bill),
    // not a hold — look-up stays glued (not a shout). Night roost
    // skip. Sit land-ruffle hang is independently gated
    // (`landSettleWattleAmp`). Hop-turn, preen, and scratch skip. Alarm
    // glance skip (shout owns the throat). Cached wattle on Bird,
    // no Find. 0 = old glued Sit wattle (sitLookWattleAmp 0).
    private float SitLookWattle01()
    {
        if (sitLookWattleAmp < 0.01f || mode != BirdMode.Sit)
            return 0f;
        if (energyRest)
            return 0f;
        if (idleLook >= -0.01f)
            return 0f;
        if (NightRoost() > 0.35f)
            return 0f;
        if (LandSettle01() > 0.01f)
            return 0f;
        if (SitTurnHop() > 0.01f)
            return 0f;
        if (idlePreen > 0.01f)
            return 0f;
        if (idleHop > 0.01f)
            return 0f;
        if (AlarmLook() > 0.01f)
            return 0f;
        return Mathf.Clamp(-idleLook, 0f, 1f)
            * Mathf.Clamp(sitLookWattleAmp, 0f, 1.5f);
    }

    // Sit forage preen: hang the hen wattle on the nibble.
    // Picture only — Cry keeps Alarm. Attack stoop stays Attack.
    // Walk hop stays Walk. Look-down owns sitLookWattleAmp.
    // Peck grab already rides the head dip. Comb nod is
    // independently gated (`sitPreenCombAmp`). Crow unmarked.
    // Hop-turn hang is independently
    // gated (`sitTurnWattleAmp`). Scratch
    // hang is independently gated (`sitScratchWattleAmp`).
    // Wander already wrote idlePreen; wing, slit, nibble, pupil,
    // tail, and far-wing brace were live, Sit look / scratch /
    // hop-turn wattle stayed skipped, so grooming still flashed a
    // statue throat. Hang with SitPreenGape01 (the nibble), not a
    // shout. Night roost, land ruffle, hop-turn, and scratch skip.
    // Alarm glance skip (shout owns the throat). Cached wattle on
    // Bird, no Find. 0 = old glued Sit wattle (sitPreenWattleAmp 0).
    private float SitPreenWattle01()
    {
        if (sitPreenWattleAmp < 0.01f || mode != BirdMode.Sit)
            return 0f;
        if (energyRest)
            return 0f;
        if (idlePreen < 0.01f)
            return 0f;
        if (NightRoost() > 0.35f)
            return 0f;
        if (LandSettle01() > 0.01f)
            return 0f;
        if (SitTurnHop() > 0.01f)
            return 0f;
        if (idleHop > 0.01f)
            return 0f;
        if (AlarmLookSit01() > 0.01f)
            return 0f;
        return idlePreen * Mathf.Clamp(sitPreenWattleAmp, 0f, 1.5f);
    }

    // Sit forage scratch: hang the hen wattle on the kick.
    // Picture only — Cry keeps Alarm. Attack stoop stays Attack.
    // Walk hop stays Walk. Look-down owns sitLookWattleAmp.
    // Preen nibble owns sitPreenWattleAmp. Peck grab already rides
    // the head dip. Comb nod is independently gated
    // (`sitScratchCombAmp`). Crow unmarked.
    // Hop-turn hang is independently gated (`sitTurnWattleAmp`).
    // Wander already wrote idleHop; foot, slit, pupil, wing,
    // bill, and tail were live, Sit look / preen / hop-turn
    // wattle stayed skipped, so scratch still flashed a statue
    // throat on the kick. Hang with SitScratchGape01 (the kick),
    // not a shout. Night roost, land ruffle, and hop-turn skip.
    // Alarm glance skip (shout owns the throat). Cached wattle
    // on Bird, no Find.
    // 0 = old glued Sit wattle (sitScratchWattleAmp 0).
    private float SitScratchWattle01()
    {
        if (sitScratchWattleAmp < 0.01f || sitScratchAmp < 0.01f
            || mode != BirdMode.Sit)
            return 0f;
        if (energyRest)
            return 0f;
        if (idleHop < 0.01f)
            return 0f;
        if (NightRoost() > 0.35f)
            return 0f;
        if (LandSettle01() > 0.01f)
            return 0f;
        if (SitTurnHop() > 0.01f)
            return 0f;
        if (AlarmLookSit01() > 0.01f)
            return 0f;
        return idleHop * Mathf.Clamp(sitScratchWattleAmp, 0f, 1.5f);
    }

    // Sit hop-turn: hang the hen wattle through the plant.
    // Picture only — Cry keeps Alarm. Attack stoop stays Attack.
    // Walk hop stays Walk. Look-down owns sitLookWattleAmp.
    // Preen nibble owns sitPreenWattleAmp. Scratch hang owns
    // sitScratchWattleAmp. Peck grab already rides the head dip.
    // Comb nod is independently gated (`sitTurnCombAmp`). Crow
    // unmarked. Walk hop-turn hang is independently gated
    // (`walkTurnWattleAmp`). Wander
    // already wrote SetFacing; legs, neck, tail, wings, pupil,
    // lid, and bill hop, Sit look / preen / scratch wattle stayed
    // skipped, so the about-face still flashed a statue throat on
    // the perch drop. Hang with SitTurnGape01 (hold through the
    // plant), not a shout. Night roost and land ruffle skip
    // (SitTurnHop). Alarm glance skip (shout owns the throat).
    // Cached wattle on Bird, no Find.
    // 0 = old glued Sit wattle (sitTurnWattleAmp 0).
    private float SitTurnWattle01()
    {
        if (sitTurnWattleAmp < 0.01f || mode != BirdMode.Sit)
            return 0f;
        if (energyRest)
            return 0f;
        if (SitTurnHop() < 0.01f)
            return 0f;
        if (AlarmLook() > 0.01f)
            return 0f;
        return Mathf.Clamp(sitTurnWattleAmp, 0f, 1.5f);
    }

    // Sit after Fly/Glide/Attack: hang the hen wattle on the plant.
    // Picture only — Cry keeps Alarm. Attack stoop stays Attack.
    // Walk hop stays Walk. Sit look / preen / scratch / hop-turn
    // keep their hang (they skip this window). Peck grab already
    // rides the head dip. Comb nod is independently gated
    // (`landSettleCombAmp`). Crow unmarked. Wings, neck, pupil, lid,
    // bill, and tail already ruffle; the throat mark stayed glued
    // to Head Visual, so Chicken Sit still flashed a statue wattle
    // on the plant. Hang with LandSettleGape01 (the plant), not a
    // shout. Night roost skip (LandSettle01). Hop-turn skip.
    // Alarm glance skip (shout owns the throat). Cached wattle
    // on Bird, no Find.
    // 0 = old glued Sit wattle (landSettleWattleAmp 0).
    private float LandSettleWattle01()
    {
        if (landSettleWattleAmp < 0.01f || mode != BirdMode.Sit)
            return 0f;
        if (energyRest)
            return 0f;
        if (SitTurnHop() > 0.01f)
            return 0f;
        if (AlarmLook() > 0.01f)
            return 0f;
        float t = LandSettle01();
        if (t < 0.02f)
            return 0f;
        float settle = Mathf.Max(0.01f, Mathf.Clamp(landSettleAmp, 0f, 1.5f));
        return Mathf.Clamp(landSettleWattleAmp, 0f, 1.5f) * (t / settle);
    }

    // Dirt Peck swallow: hang the hen wattle on the toss.
    // Picture only — Cry keeps Alarm. Attack stoop stays Attack.
    // Walk hop / Walk hop-turn stay Walk. Sit look / preen /
    // scratch / hop-turn / land-ruffle keep their hang. Peck grab
    // still rides the head dip (the jab). Comb nod is independently
    // gated (`peckCombAmp`). Crow unmarked. Crop already bulges
    // on the toss (`peckCropAmp`); the throat mark stayed glued to
    // Head Visual, so Chicken Peck still flashed a statue wattle
    // on the swallow. Hang with PeckSwallow01 (the toss), not a
    // shout. Woodpecker drum skip. Alarm glance skip (shout owns
    // the throat). Cached wattle on Bird, no Find.
    // 0 = old glued Peck wattle (peckWattleAmp 0).
    private float PeckWattle01()
    {
        if (peckWattleAmp < 0.01f || mode != BirdMode.Peck)
            return 0f;
        if (energyRest)
            return 0f;
        if (IsWoodpeckerDrum())
            return 0f;
        if (AlarmLook() > 0.01f)
            return 0f;
        float u = PeckSwallow01();
        if (u < 0.02f)
            return 0f;
        return u * Mathf.Clamp(peckWattleAmp, 0f, 1.5f);
    }

    // Fly first air: hang the hen wattle on the climb. Picture
    // only — Cry keeps Alarm. Attack stoop stays Attack. Walk hop
    // / Walk hop-turn stay Walk. Sit look / preen / scratch /
    // hop-turn / land-ruffle keep their hang. Peck swallow stays
    // Peck. Peck grab still rides the head dip. Comb nod is
    // independently gated (`takeoffCombAmp`). Crow unmarked. Legs,
    // wings, tail, pupil, lid, and bill already climb
    // (`takeoffGapeAmp`); the throat mark stayed glued to Head
    // Visual, so Chicken Fly still flashed a statue wattle on
    // the hop. Hang with the first-air clock (leave is
    // strongest), not a shout. Dirt coil skip. Sinking skip
    // (flare owns landWattleAmp; swoop wattle stays glued). Flare
    // owns landGapeAmp. Swoop owns swoopGapeAmp. Air turn owns
    // airTurnGapeAmp. Sit ruffle owns landSettleGapeAmp. Alarm
    // glance skip (shout owns the throat). Cached wattle on
    // Bird, no Find.
    // 0 = old glued takeoff wattle (takeoffWattleAmp 0).
    private float TakeoffWattle01()
    {
        if (takeoffWattleAmp < 0.01f || mode != BirdMode.Fly)
            return 0f;
        if (energyRest)
            return 0f;
        if (OnDirtSupport())
            return 0f;
        if (bodyRb != null && bodyRb.linearVelocity.y < -0.08f)
            return 0f;
        if (AlarmLook() > 0.01f)
            return 0f;
        if (Time.time >= takeoffWattleUntil)
            return 0f;
        float dur = takeoffWattleUntil - takeoffWattleBorn;
        if (dur < 0.05f)
            return 0f;
        return Mathf.Clamp(takeoffWattleAmp, 0f, 1.5f)
            * (1f - Mathf.Clamp01((Time.time - takeoffWattleBorn) / dur));
    }

    // Fly/Glide flare: hang the hen wattle on the plant. Picture
    // only — Cry keeps Alarm. Attack stoop stays Attack. Walk hop
    // / Walk hop-turn stay Walk. Sit look / preen / scratch /
    // hop-turn / land-ruffle keep their hang. Peck swallow stays
    // Peck. Peck grab still rides the head dip. Comb nod is
    // independently gated (`landCombAmp`). Crow unmarked. Gear,
    // cup, tail, pupil, lid, and bill already read the flare
    // (`landGapeAmp`); the throat mark stayed glued to Head Visual,
    // so Chicken Fly/Glide still flashed a statue wattle on the
    // drop. Hang with flareLook (closer is stronger), not a shout.
    // Skip climb and dirt takeoff (vy and trail). Takeoff climb
    // owns takeoffWattleAmp. Swoop wattle stays glued. Air turn
    // owns airTurnGapeAmp. Sit ruffle owns landSettleWattleAmp.
    // Alarm glance skip (shout owns the throat). Cached wattle on
    // Bird, no Find.
    // 0 = old glued flare wattle (landWattleAmp 0).
    private float LandWattle01()
    {
        if (landWattleAmp < 0.01f)
            return 0f;
        if (mode != BirdMode.Fly && mode != BirdMode.Glide)
            return 0f;
        if (energyRest)
            return 0f;
        if (OnDirtSupport())
            return 0f;
        if (TakeoffWing01() > 0.01f || TakeoffTrail01() > 0.01f)
            return 0f;
        if (bodyRb != null && bodyRb.linearVelocity.y > -0.08f)
            return 0f;
        if (AlarmLook() > 0.01f)
            return 0f;
        if (flight == null)
            return 0f;
        float t = flight.flareLook;
        if (t < 0.02f)
            return 0f;
        return Mathf.Clamp(landWattleAmp, 0f, 1.5f) * t;
    }

    // Walk hop-turn: hang the hen wattle through the plant.
    // Picture only — Cry keeps Alarm. Attack stoop stays Attack.
    // Walk hop stays Walk (WalkWattle01 skips WalkTurnHop). Sit
    // look / preen / scratch / hop-turn stay Sit. Peck grab
    // already rides the head dip. Comb nod is independently
    // gated (`walkTurnCombAmp`). Crow unmarked. Wander already
    // wrote SetFacing; legs, neck, tail, wings, pupil, lid, and
    // bill hop, Walk hop wattle stayed skipped, so the about-face
    // still flashed a statue throat on the dirt drop. Hang with
    // WalkTurnGape01 (hold through the plant), not a shout. Alarm
    // glance skip (shout owns the throat). Cached wattle on Bird,
    // no Find.
    // 0 = old glued Walk wattle (walkTurnWattleAmp 0).
    private float WalkTurnWattle01()
    {
        if (walkTurnWattleAmp < 0.01f || mode != BirdMode.Walk)
            return 0f;
        if (energyRest)
            return 0f;
        if (WalkTurnHop() < 0.01f)
            return 0f;
        if (AlarmLook() > 0.01f)
            return 0f;
        return Mathf.Clamp(walkTurnWattleAmp, 0f, 1.5f);
    }

    // BIRD-6 Alarm/Call: nod the hen comb with the 0.55 s shout.
    // Picture only — wattle hang stays cryWattleAmp (flesh drops;
    // cartilage erects). Crow / woodpecker have no mark. Peck grab
    // already rides the head dip. Attack stoop stays Attack
    // (contrast — shout nod is cryCombAmp). Glance nod is
    // independently gated (`alarmLookCombAmp`). Glance 8° stays glance.
    // Gape, crop, tongue, and wattle were live; without this the
    // Chicken scene still flashed a glued crown. Scale with
    // CryGape, not AlarmLook. Hen Walk hop reuses it
    // (`walkCombAmp`) — legs, wings, tail, eye, lid, bill, and
    // wattle already hop, the crown stayed glued, so Chicken Walk
    // still flashed a statue comb. Same WalkHopDown01 tuck as
    // walkWattleAmp (erect vs hang). Walk hop-turn reuses it
    // (`walkTurnCombAmp`) — legs, neck, tail, wings, pupil, lid,
    // bill, and wattle already hop, Walk hop comb stayed skipped,
    // so the about-face still flashed a statue comb on the dirt
    // drop. Same WalkTurnGape01 hold as walkTurnWattleAmp (erect
    // vs hang). Sit hop-turn reuses it (`sitTurnCombAmp`) — legs,
    // neck, tail, wings, pupil, lid, bill, and wattle already hop,
    // Sit look / preen / scratch comb stayed skipped, so the
    // about-face still flashed a statue comb on the perch drop.
    // Same SitTurnGape01 hold as sitTurnWattleAmp (erect vs hang).
    // Sit idleLook reuses it (`sitLookCombAmp`) — neck, pupil,
    // tail, wing, bill, and wattle already weave, Sit look / preen
    // / scratch comb stayed skipped, so look-down still flashed a
    // statue comb on the perch. Same SitLookGape01 clock as
    // sitLookWattleAmp (erect vs hang, toward the bill).
    // Sit idlePreen reuses it (`sitPreenCombAmp`) — wing, slit,
    // nibble, pupil, tail, far-wing brace, and wattle already
    // reach, Sit look / scratch comb stayed skipped, so the
    // nibble still flashed a statue comb on the perch. Same
    // SitPreenGape01 clock as sitPreenWattleAmp (erect vs hang).
    // Sit idleHop reuses it (`sitScratchCombAmp`) — foot, slit,
    // pupil, wing, bill, tail, and wattle already kick, Sit look
    // / preen comb stayed skipped, so the kick still flashed a
    // statue comb on the perch. Same SitScratchGape01 clock as
    // sitScratchWattleAmp (erect vs hang).
    // Sit land-ruffle reuses it (`landSettleCombAmp`) — wings,
    // neck, pupil, lid, bill, tail, and wattle already shake,
    // Sit look / preen / scratch / hop-turn comb stayed skipped,
    // so the plant still flashed a statue comb. Same
    // LandSettleGape01 plant as landSettleWattleAmp (erect vs
    // hang).
    // Dirt Peck swallow reuses it (`peckCombAmp`) — crop and
    // wattle already live on the toss, the crown stayed glued to
    // Head Visual, so Chicken Peck still flashed a statue comb
    // over a live throat. Same PeckSwallow01 toss as
    // peckWattleAmp (erect vs hang). Peck grab still rides the
    // head dip. Woodpecker drum skip.
    // Fly first-air reuses it (`takeoffCombAmp`) — legs, wings,
    // tail, pupil, lid, bill, and wattle already climb, the crown
    // stayed glued to Head Visual, so Chicken Fly still flashed
    // a statue comb over a live throat. Same first-air clock as
    // takeoffWattleAmp (erect vs hang). Dirt coil skip. Sinking
    // skip (flare owns landWattleAmp / landCombAmp; swoop comb
    // stays glued).
    // Fly/Glide flare reuses it (`landCombAmp`) — gear, cup, tail,
    // pupil, lid, bill, and wattle already drop, the crown stayed
    // glued to Head Visual, so Chicken Fly/Glide still flashed a
    // statue comb over a live throat. Same flareLook as
    // landWattleAmp (erect vs hang). Climb and dirt takeoff skip.
    // Alarm glance reuses it (`alarmLookCombAmp`) — neck, tail,
    // pupil, lid, bill, wings, and wattle already glance after the
    // 0.55 s shout; the crown stayed glued to Head Visual, so
    // Chicken Flee still flashed a statue comb over a live throat
    // on the 1.15 s look-back. Same AlarmLook as AlarmLookGape01 /
    // AlarmLookWattle01. Cry shout stays Cry (listed first).
    // Attack stoop stays glued (contrast). Wattle hang stays
    // alarmLookWattleAmp (flesh drops; cartilage erects). Swoop
    // comb stays glued. Cry wins, then Alarm glance, then Walk hop,
    // then Walk hop-turn, then Sit hop-turn, then Sit look, then
    // Sit preen, then Sit scratch, then Sit land-ruffle, then Peck
    // swallow, then takeoff climb, then land-flare.
    // Cached comb on Bird, no Find.
    // 0 = old glued comb (cryCombAmp 0 / alarmLookCombAmp 0 /
    // walkCombAmp 0 /
    // walkTurnCombAmp 0 / sitTurnCombAmp 0 / sitLookCombAmp 0 /
    // sitPreenCombAmp 0 / sitScratchCombAmp 0 /
    // landSettleCombAmp 0 / peckCombAmp 0 / takeoffCombAmp 0 /
    // landCombAmp 0).
    private void ApplyCryComb()
    {
        if (bird == null)
            return;
        if (bird.combFrontRenderer == null && bird.combBackRenderer == null)
            return;
        float a = CryComb01();
        if (a < 0.04f)
            a = AlarmLookComb01();
        if (a < 0.04f)
            a = WalkComb01();
        if (a < 0.04f)
            a = WalkTurnComb01();
        if (a < 0.04f)
            a = SitTurnComb01();
        if (a < 0.04f)
            a = SitLookComb01();
        if (a < 0.04f)
            a = SitPreenComb01();
        if (a < 0.04f)
            a = SitScratchComb01();
        if (a < 0.04f)
            a = LandSettleComb01();
        if (a < 0.04f)
            a = PeckComb01();
        if (a < 0.04f)
            a = TakeoffComb01();
        if (a < 0.04f)
            a = LandComb01();
        bool on = a > 0.04f;
        if (!on)
        {
            if (!lastCombOn)
                return;
            RestComb(bird.combFrontRenderer, bird.combFrontRestLocal, bird.combFrontRestSize);
            RestComb(bird.combBackRenderer, bird.combBackRestLocal, bird.combBackRestSize);
            lastCombOn = false;
            lastCombAmt = float.NaN;
            return;
        }

        if (lastCombOn && Mathf.Abs(a - lastCombAmt) < 0.01f)
            return;
        lastCombOn = true;
        lastCombAmt = a;
        PoseComb(bird.combFrontRenderer, bird.combFrontRestLocal, bird.combFrontRestSize,
            a, 0.40f, 0.22f, 10f);
        PoseComb(bird.combBackRenderer, bird.combBackRestLocal, bird.combBackRestSize,
            a, 0.28f, 0.16f, 7f);
    }

    private float CryComb01()
    {
        if (cryCombAmp < 0.01f)
            return 0f;
        if (energyRest)
            return 0f;
        if (mode == BirdMode.Peck || mode == BirdMode.Attack)
            return 0f;
        float g = CryGape();
        if (g < 0.01f)
            return 0f;
        return g * Mathf.Clamp(cryCombAmp, 0f, 1.5f);
    }

    // BIRD-6 Flee: nod the hen comb through the glance after
    // cry. Picture only — Cry keeps Alarm (the 0.55 s shout,
    // listed first). Attack stoop stays glued (contrast). Walk hop /
    // Walk hop-turn / Sit look / preen / scratch / hop-turn /
    // land-ruffle keep their nod (those skip AlarmLook). Peck
    // grab already rides the head dip. Wattle hang stays
    // alarmLookWattleAmp (flesh drops; cartilage erects). Crow
    // unmarked. Neck, tail, pupil, lid, bill, wings, and wattle
    // already glance; the crown stayed glued to Head Visual, so
    // Chicken Flee still flashed a statue comb over a live throat
    // on the 1.15 s look-back after the shout faded. Erect with
    // AlarmLook (same envelope as AlarmLookGape01 /
    // AlarmLookWattle01), not a shout. Night roost skip. Swoop
    // comb stays glued (hens skip swoop; crow unmarked). Cached
    // comb on Bird, no Find.
    // 0 = old glued glance comb (alarmLookCombAmp 0).
    private float AlarmLookComb01()
    {
        if (alarmLookCombAmp < 0.01f)
            return 0f;
        if (energyRest)
            return 0f;
        if (NightRoost() > 0.35f)
            return 0f;
        float look = AlarmLook();
        if (look < 0.02f)
            return 0f;
        float a = Mathf.Max(0.01f, Mathf.Clamp(alarmLookAmp, 0f, 1.5f));
        return Mathf.Clamp(alarmLookCombAmp, 0f, 1.5f) * (look / a);
    }

    // Hen Walk hop: nod the comb on the tuck. Picture only —
    // walkThrust still translates. Cry keeps Alarm. Attack stoop
    // stays Attack (contrast). Peck grab already rides the head
    // dip. Wattle hang stays walkWattleAmp (flesh drops;
    // cartilage erects). Crow stride has no mark (`walkHopAmp`
    // 0). Alarm glance nod is independently gated
    // (`alarmLookCombAmp`); this skip AlarmLook so the look-back
    // owns the crown. Walk hop-turn
    // nod is independently gated (`walkTurnCombAmp`) — this skip
    // WalkTurnHop so the about-face owns the crown. Legs, wings,
    // tail, eye, lid, bill, and wattle already hop; without this
    // the Chicken commute still flashed a statue crown. Erect with
    // WalkHopDown01 (same tuck as walkWattleAmp). Cached comb on
    // Bird, no Find.
    // 0 = old glued Walk comb (walkCombAmp 0).
    private float WalkComb01()
    {
        if (walkCombAmp < 0.01f)
            return 0f;
        if (energyRest)
            return 0f;
        if (mode != BirdMode.Walk)
            return 0f;
        if (walkHopAmp < 0.01f)
            return 0f;
        if (WalkTurnHop() > 0.01f)
            return 0f;
        if (AlarmLook() > 0.01f)
            return 0f;
        float nod = 1f - WalkHopDown01();
        if (nod < 0.04f)
            return 0f;
        return nod
            * Mathf.Clamp(walkCombAmp, 0f, 1.5f)
            * Mathf.Clamp(walkHopAmp, 0f, 1.5f);
    }

    // Walk hop-turn: nod the hen comb through the plant.
    // Picture only — Cry keeps Alarm. Attack stoop stays Attack
    // (contrast). Walk hop stays Walk (WalkComb01 skips
    // WalkTurnHop). Sit hop-turn nod is independently gated
    // (`sitTurnCombAmp`). Sit look nod is independently gated
    // (`sitLookCombAmp`). Sit preen nod is independently gated
    // (`sitPreenCombAmp`). Sit scratch nod is independently gated
    // (`sitScratchCombAmp`). Peck grab already rides the head dip.
    // Wattle hang stays walkTurnWattleAmp (flesh drops;
    // cartilage erects). Crow unmarked. Wander already wrote
    // SetFacing; legs, neck, tail, wings, pupil, lid, bill, and
    // wattle hop, Walk hop comb stayed skipped, so the about-face
    // still flashed a statue crown on the dirt drop. Erect with
    // WalkTurnGape01 (hold through the plant), not a shout. Alarm
    // glance nod is independently gated (`alarmLookCombAmp`). Cached comb on Bird, no
    // Find.
    // 0 = old glued Walk comb (walkTurnCombAmp 0).
    private float WalkTurnComb01()
    {
        if (walkTurnCombAmp < 0.01f || mode != BirdMode.Walk)
            return 0f;
        if (energyRest)
            return 0f;
        if (WalkTurnHop() < 0.01f)
            return 0f;
        if (AlarmLook() > 0.01f)
            return 0f;
        return Mathf.Clamp(walkTurnCombAmp, 0f, 1.5f);
    }

    // Sit hop-turn: nod the hen comb through the plant.
    // Picture only — Cry keeps Alarm. Attack stoop stays Attack
    // (contrast). Walk hop / Walk hop-turn stay Walk. Sit look
    // nod is independently gated (`sitLookCombAmp`). Sit preen
    // nod is independently gated (`sitPreenCombAmp`). Sit
    // scratch nod is independently gated (`sitScratchCombAmp`).
    // Peck grab already
    // rides the head dip. Wattle hang stays sitTurnWattleAmp
    // (flesh drops; cartilage erects). Crow unmarked. Wander
    // already wrote SetFacing; legs, neck, tail, wings, pupil,
    // lid, bill, and wattle hop, Sit look / preen / scratch comb
    // stayed skipped, so the about-face still flashed a statue
    // crown on the perch drop. Erect with SitTurnGape01 (hold
    // through the plant), not a shout. Night roost and land
    // ruffle skip (SitTurnHop). Alarm glance nod is independently
    // gated (`alarmLookCombAmp`). Cached comb on Bird, no Find.
    // 0 = old glued Sit comb (sitTurnCombAmp 0).
    private float SitTurnComb01()
    {
        if (sitTurnCombAmp < 0.01f || mode != BirdMode.Sit)
            return 0f;
        if (energyRest)
            return 0f;
        if (SitTurnHop() < 0.01f)
            return 0f;
        if (AlarmLook() > 0.01f)
            return 0f;
        return Mathf.Clamp(sitTurnCombAmp, 0f, 1.5f);
    }

    // Sit forage look-around: nod the hen comb on look-down.
    // Picture only — Cry keeps Alarm. Attack stoop stays Attack
    // (contrast). Walk hop / Walk hop-turn stay Walk. Sit hop-turn
    // nod is independently gated (`sitTurnCombAmp`). Sit preen
    // nod is independently gated (`sitPreenCombAmp`). Sit
    // scratch nod is independently gated (`sitScratchCombAmp`).
    // Peck grab already rides the
    // head dip. Wattle hang stays sitLookWattleAmp (flesh drops;
    // cartilage erects). Crow unmarked. Wander already wrote
    // idleLook; neck, pupil, tail, wing, bill, and wattle weave,
    // Sit look / preen / scratch comb stayed skipped, so look-down
    // still flashed a statue crown on the perch. Erect with
    // SitLookGape01 (toward the bill), not a shout — look-up stays
    // glued (not a call). Night roost, land ruffle, hop-turn,
    // preen, and scratch skip. Alarm glance nod is independently
    // gated (`alarmLookCombAmp`). Cached comb on Bird, no Find.
    // 0 = old glued Sit comb (sitLookCombAmp 0).
    private float SitLookComb01()
    {
        if (sitLookCombAmp < 0.01f || mode != BirdMode.Sit)
            return 0f;
        if (energyRest)
            return 0f;
        if (idleLook >= -0.01f)
            return 0f;
        if (NightRoost() > 0.35f)
            return 0f;
        if (LandSettle01() > 0.01f)
            return 0f;
        if (SitTurnHop() > 0.01f)
            return 0f;
        if (idlePreen > 0.01f)
            return 0f;
        if (idleHop > 0.01f)
            return 0f;
        if (AlarmLook() > 0.01f)
            return 0f;
        return Mathf.Clamp(-idleLook, 0f, 1f)
            * Mathf.Clamp(sitLookCombAmp, 0f, 1.5f);
    }

    // Sit forage preen: nod the hen comb on the nibble.
    // Picture only — Cry keeps Alarm. Attack stoop stays Attack
    // (contrast). Walk hop / Walk hop-turn stay Walk. Sit look
    // nod is independently gated (`sitLookCombAmp`). Sit hop-turn
    // nod is independently gated (`sitTurnCombAmp`). Sit scratch
    // nod is independently gated (`sitScratchCombAmp`). Peck grab
    // already rides the head
    // dip. Wattle hang stays sitPreenWattleAmp (flesh drops;
    // cartilage erects). Crow unmarked. Wander already wrote
    // idlePreen; wing, slit, nibble, pupil, tail, far-wing brace,
    // and wattle were live, Sit look / scratch comb stayed
    // skipped, so grooming still flashed a statue crown on the
    // perch. Erect with SitPreenGape01 (the nibble), not a shout.
    // Night roost, land ruffle, hop-turn, and scratch skip. Alarm
    // glance nod is independently gated (`alarmLookCombAmp`). Cached comb on Bird, no
    // Find.
    // 0 = old glued Sit comb (sitPreenCombAmp 0).
    private float SitPreenComb01()
    {
        if (sitPreenCombAmp < 0.01f || mode != BirdMode.Sit)
            return 0f;
        if (energyRest)
            return 0f;
        if (idlePreen < 0.01f)
            return 0f;
        if (NightRoost() > 0.35f)
            return 0f;
        if (LandSettle01() > 0.01f)
            return 0f;
        if (SitTurnHop() > 0.01f)
            return 0f;
        if (idleHop > 0.01f)
            return 0f;
        if (AlarmLookSit01() > 0.01f)
            return 0f;
        return idlePreen * Mathf.Clamp(sitPreenCombAmp, 0f, 1.5f);
    }

    // Sit forage scratch: nod the hen comb on the kick.
    // Picture only — Cry keeps Alarm. Attack stoop stays Attack
    // (contrast). Walk hop / Walk hop-turn stay Walk. Sit look
    // nod is independently gated (`sitLookCombAmp`). Sit hop-turn
    // nod is independently gated (`sitTurnCombAmp`). Sit preen
    // nod is independently gated (`sitPreenCombAmp`). Peck grab
    // already rides the head dip. Wattle hang stays
    // sitScratchWattleAmp (flesh drops; cartilage erects). Crow
    // unmarked. Wander already wrote idleHop; foot, slit, pupil,
    // wing, bill, tail, and wattle were live, Sit look / preen
    // comb stayed skipped, so scratch still flashed a statue
    // crown on the perch. Erect with SitScratchGape01 (the kick),
    // not a shout. Night roost, land ruffle, and hop-turn skip.
    // Alarm glance nod is independently gated (`alarmLookCombAmp`). Cached comb on
    // Bird, no Find.
    // 0 = old glued Sit comb (sitScratchCombAmp 0).
    private float SitScratchComb01()
    {
        if (sitScratchCombAmp < 0.01f || sitScratchAmp < 0.01f
            || mode != BirdMode.Sit)
            return 0f;
        if (energyRest)
            return 0f;
        if (idleHop < 0.01f)
            return 0f;
        if (NightRoost() > 0.35f)
            return 0f;
        if (LandSettle01() > 0.01f)
            return 0f;
        if (SitTurnHop() > 0.01f)
            return 0f;
        if (AlarmLookSit01() > 0.01f)
            return 0f;
        return idleHop * Mathf.Clamp(sitScratchCombAmp, 0f, 1.5f);
    }

    // Sit after Fly/Glide/Attack: nod the hen comb on the plant.
    // Picture only — Cry keeps Alarm. Attack stoop stays Attack
    // (contrast). Walk hop / Walk hop-turn stay Walk. Sit look /
    // preen / scratch / hop-turn keep their nod (they skip this
    // window). Peck swallow nod is independently gated
    // (`peckCombAmp`). Peck grab already rides the head dip.
    // Wattle hang stays landSettleWattleAmp (flesh drops;
    // cartilage erects).
    // Crow unmarked. Wings, neck, pupil, lid, bill, tail, and
    // wattle already ruffle; the crown stayed glued to Head
    // Visual, so Chicken Sit still flashed a statue comb on the
    // plant. Erect with LandSettleGape01 (the plant), not a shout.
    // Night roost skip (LandSettle01). Hop-turn skip. Alarm
    // glance nod is independently gated (`alarmLookCombAmp`). Cached comb on Bird, no
    // Find.
    // 0 = old glued Sit comb (landSettleCombAmp 0).
    private float LandSettleComb01()
    {
        if (landSettleCombAmp < 0.01f || mode != BirdMode.Sit)
            return 0f;
        if (energyRest)
            return 0f;
        if (SitTurnHop() > 0.01f)
            return 0f;
        if (AlarmLook() > 0.01f)
            return 0f;
        float t = LandSettle01();
        if (t < 0.02f)
            return 0f;
        float settle = Mathf.Max(0.01f, Mathf.Clamp(landSettleAmp, 0f, 1.5f));
        return Mathf.Clamp(landSettleCombAmp, 0f, 1.5f) * (t / settle);
    }

    // Dirt Peck swallow: nod the hen comb on the toss.
    // Picture only — Cry keeps Alarm. Attack stoop stays Attack
    // (contrast). Walk hop / Walk hop-turn stay Walk. Sit look /
    // preen / scratch / hop-turn / land-ruffle keep their nod.
    // Peck grab still rides the head dip (the jab). Wattle hang
    // stays peckWattleAmp (flesh drops; cartilage erects). Crow
    // unmarked. Crop and wattle already live on the toss; the
    // crown stayed glued to Head Visual, so Chicken Peck still
    // flashed a statue comb over a live throat. Erect with
    // PeckSwallow01 (the toss), not a shout. Woodpecker drum skip.
    // Alarm glance nod is independently gated (`alarmLookCombAmp`). Cached comb on
    // Bird, no Find.
    // 0 = old glued Peck comb (peckCombAmp 0).
    private float PeckComb01()
    {
        if (peckCombAmp < 0.01f || mode != BirdMode.Peck)
            return 0f;
        if (energyRest)
            return 0f;
        if (IsWoodpeckerDrum())
            return 0f;
        if (AlarmLook() > 0.01f)
            return 0f;
        float u = PeckSwallow01();
        if (u < 0.02f)
            return 0f;
        return u * Mathf.Clamp(peckCombAmp, 0f, 1.5f);
    }

    // Fly first air: nod the hen comb on the climb. Picture
    // only — Cry keeps Alarm. Attack stoop stays glued (contrast).
    // Walk hop / Walk hop-turn stay Walk. Sit look / preen /
    // scratch / hop-turn / land-ruffle keep their nod. Peck swallow
    // stays Peck. Peck grab still rides the head dip. Wattle hang
    // stays takeoffWattleAmp (flesh drops; cartilage erects). Crow
    // unmarked. Legs, wings, tail, pupil, lid, bill, and wattle
    // already climb; the crown stayed glued to Head Visual, so
    // Chicken Fly still flashed a statue comb over a live throat.
    // Erect with the first-air clock (leave is strongest), not a
    // shout. Dirt coil skip. Sinking skip (flare owns
    // landWattleAmp / landCombAmp; swoop comb stays glued). Flare
    // owns landGapeAmp. Swoop owns swoopGapeAmp. Air turn owns
    // airTurnGapeAmp. Sit ruffle owns landSettleGapeAmp. Alarm
    // glance nod is independently gated (`alarmLookCombAmp`).
    // Cached comb on Bird, no Find.
    // 0 = old glued takeoff comb (takeoffCombAmp 0).
    private float TakeoffComb01()
    {
        if (takeoffCombAmp < 0.01f || mode != BirdMode.Fly)
            return 0f;
        if (energyRest)
            return 0f;
        if (OnDirtSupport())
            return 0f;
        if (bodyRb != null && bodyRb.linearVelocity.y < -0.08f)
            return 0f;
        if (AlarmLook() > 0.01f)
            return 0f;
        if (Time.time >= takeoffCombUntil)
            return 0f;
        float dur = takeoffCombUntil - takeoffCombBorn;
        if (dur < 0.05f)
            return 0f;
        return Mathf.Clamp(takeoffCombAmp, 0f, 1.5f)
            * (1f - Mathf.Clamp01((Time.time - takeoffCombBorn) / dur));
    }

    // Fly/Glide flare: nod the hen comb on the plant. Picture only
    // — Cry keeps Alarm. Attack stoop stays glued (contrast). Walk
    // hop / Walk hop-turn stay Walk. Sit look / preen / scratch /
    // hop-turn / land-ruffle keep their nod. Peck swallow stays
    // Peck. Peck grab still rides the head dip. Wattle hang stays
    // landWattleAmp (flesh drops; cartilage erects). Crow unmarked.
    // Gear, cup, tail, pupil, lid, bill, and wattle already drop;
    // the crown stayed glued to Head Visual, so Chicken Fly/Glide
    // still flashed a statue comb over a live throat. Erect with
    // flareLook (closer is stronger), not a shout. Skip climb and
    // dirt takeoff (vy and trail). Takeoff climb owns
    // takeoffCombAmp. Swoop comb stays glued. Air turn owns
    // airTurnGapeAmp. Sit ruffle owns landSettleCombAmp. Alarm
    // glance nod is independently gated (`alarmLookCombAmp`). Cached comb on Bird, no
    // Find.
    // 0 = old glued flare comb (landCombAmp 0).
    private float LandComb01()
    {
        if (landCombAmp < 0.01f)
            return 0f;
        if (mode != BirdMode.Fly && mode != BirdMode.Glide)
            return 0f;
        if (energyRest)
            return 0f;
        if (OnDirtSupport())
            return 0f;
        if (TakeoffWing01() > 0.01f || TakeoffTrail01() > 0.01f)
            return 0f;
        if (bodyRb != null && bodyRb.linearVelocity.y > -0.08f)
            return 0f;
        if (AlarmLook() > 0.01f)
            return 0f;
        if (flight == null)
            return 0f;
        float t = flight.flareLook;
        if (t < 0.02f)
            return 0f;
        return Mathf.Clamp(landCombAmp, 0f, 1.5f) * t;
    }

    private static void RestComb(SpriteRenderer sr, Vector3 restLocal, Vector2 restSize)
    {
        if (sr == null)
            return;
        sr.size = restSize;
        sr.transform.localPosition = restLocal;
        sr.transform.localRotation = Quaternion.identity;
    }

    private static void PoseComb(
        SpriteRenderer sr, Vector3 restLocal, Vector2 restSize,
        float a, float stretchY, float liftY, float tilt)
    {
        if (sr == null)
            return;
        sr.size = new Vector2(restSize.x * (1f + 0.08f * a), restSize.y * (1f + stretchY * a));
        Vector3 pos = restLocal;
        pos.y += restSize.y * liftY * a;
        sr.transform.localPosition = pos;
        sr.transform.localRotation = Quaternion.Euler(0f, 0f, tilt * a);
    }

    // Dirt Walk forage: part the bill toward the soil. Picture only —
    // Peck Cos and Attack stoop keep their own gape. Cry keeps Alarm.
    // Sit idlePreen has its own part (`sitPreenGapeAmp`). Sit
    // idleLook has its own look-down part (`sitLookGapeAmp`). Sit
    // idleHop scratch has its own kick part (`sitScratchGapeAmp`).
    // The lid still left a shut beak on the dirt, so forage still
    // read as a commute with a squint. Held ~8°, not a grab. Walk
    // Fly/Glide air turn reuses it on the bank (`airTurnGapeAmp`).
    // Landing flare reuses it on the plant (`landGapeAmp`). Dirt
    // swoop reuses it on the dive (`swoopGapeAmp`). Takeoff
    // climb reuses it on the hop (`takeoffGapeAmp`). Sit
    // land-ruffle reuses it on the plant (`landSettleGapeAmp`).
    // Alarm glance reuses it on Flee (`alarmLookGapeAmp`).
    // Hen hop holds it on plant. No Find.
    // 0 = old shut bill (walkGapeAmp 0) / shut-bill hop-turn
    // (walkTurnGapeAmp 0) / shut-bill Sit hop-turn (sitTurnGapeAmp 0)
    // / shut-bill air turn (airTurnGapeAmp 0) / shut-bill land
    // flare (landGapeAmp 0) / shut-bill dirt swoop
    // (swoopGapeAmp 0) / shut-bill takeoff climb
    // (takeoffGapeAmp 0) / shut-bill Sit land-ruffle
    // (landSettleGapeAmp 0) / shut-bill Alarm glance
    // (alarmLookGapeAmp 0).
    private float WalkScanGape01()
    {
        if (walkGapeAmp < 0.01f || mode != BirdMode.Walk)
            return 0f;
        if (energyRest)
            return 0f;
        if (sensors != null && !sensors.bothGrounded)
            return 0f;
        if (WalkTurnHop() > 0.01f)
            return 0f;
        if (AlarmLook() > 0.01f)
            return 0f;
        return Mathf.Clamp(walkGapeAmp, 0f, 1.5f);
    }

    // Walk hop-turn: part the same bill toward the dirt. Picture
    // only — Walk scan / Peck / Sit-preen / Attack keep their own
    // gape. Cry keeps Alarm. Wander already wrote SetFacing; legs,
    // neck, tail, wings, pupil, and lid hop, Walk gape stayed
    // skipped, so the about-face still flashed a shut bill on the
    // dirt drop. Hold through the plant (same ~8° as walkGapeAmp),
    // not a pulse — sin edges would snap the shut twice. Sit
    // hop-turn owns sitTurnGapeAmp on the perch. Air turn owns
    // airTurnGapeAmp. Stride gape stays Walk. Peck Cos stays
    // Peck. Wattle hang is independently gated
    // (`walkTurnWattleAmp`). Alarm glance skip. No Find.
    // 0 = old shut bill (walkTurnGapeAmp 0).
    private float WalkTurnGape01()
    {
        if (walkTurnGapeAmp < 0.01f || mode != BirdMode.Walk)
            return 0f;
        if (energyRest)
            return 0f;
        if (WalkTurnHop() < 0.01f)
            return 0f;
        if (AlarmLook() > 0.01f)
            return 0f;
        return Mathf.Clamp(walkTurnGapeAmp, 0f, 1.5f);
    }

    // Sit hop-turn: part the same bill toward the perch. Picture
    // only — Walk / Peck / Sit-preen / Attack keep their own gape.
    // Cry keeps Alarm. Wander already wrote SetFacing; legs, neck,
    // tail, wings, pupil, and lid hop, Sit look / preen / scratch
    // stayed skipped, so the about-face still flashed a shut bill
    // on the perch drop. Hold through the plant (same ~8° as
    // walkGapeAmp), not a pulse — sin edges would snap the shut
    // twice. Walk hop-turn stays on the dirt. Air turn owns
    // airTurnGapeAmp. Stride gape stays Walk. Peck Cos stays
    // Peck. Look-down owns sitLookGapeAmp. Scratch owns
    // sitScratchGapeAmp. Wattle hang is independently gated
    // (`sitTurnWattleAmp`). Comb nod is independently gated
    // (`sitTurnCombAmp`). Walk hop-turn wattle hang is
    // independently gated (`walkTurnWattleAmp`). Alarm glance
    // skip. No Find.
    // 0 = old shut bill (sitTurnGapeAmp 0).
    private float SitTurnGape01()
    {
        if (sitTurnGapeAmp < 0.01f || mode != BirdMode.Sit)
            return 0f;
        if (energyRest)
            return 0f;
        if (SitTurnHop() < 0.01f)
            return 0f;
        if (AlarmLook() > 0.01f)
            return 0f;
        return Mathf.Clamp(sitTurnGapeAmp, 0f, 1.5f);
    }

    // Sit forage preen: part the same bill on the reach. Picture only —
    // Walk / Peck / Attack keep their own gape. Cry keeps Alarm. Wander
    // already wrote idlePreen; the slit still left a shut beak, so
    // grooming read as a squinting stretch. Nibble with idlePreen
    // (not a held Walk part). Night roost, land ruffle, and
    // hop-turn skip. Sit ruffle owns landSettleGapeAmp.
    // Hop-turn owns sitTurnGapeAmp. Look-down
    // owns sitLookGapeAmp. Scratch owns sitScratchGapeAmp.
    // No Find.
    // 0 = old shut bill (sitPreenGapeAmp 0).
    private float SitPreenGape01()
    {
        if (sitPreenGapeAmp < 0.01f || mode != BirdMode.Sit)
            return 0f;
        if (energyRest)
            return 0f;
        if (idlePreen < 0.01f)
            return 0f;
        if (NightRoost() > 0.35f)
            return 0f;
        if (LandSettle01() > 0.01f)
            return 0f;
        if (SitTurnHop() > 0.01f)
            return 0f;
        if (idleHop > 0.01f)
            return 0f;
        if (AlarmLookSit01() > 0.01f)
            return 0f;
        return idlePreen * Mathf.Clamp(sitPreenGapeAmp, 0f, 1.5f);
    }

    // Sit forage look-around: part the same bill on look-down.
    // Picture only — Walk / Peck / Sit-preen / Attack keep their
    // own gape. Cry keeps Alarm. Wander already wrote idleLook;
    // neck, pupil, tail, and wing weave, Sit look / scratch gape
    // stayed skipped, so look-down still flashed a shut bill on
    // the perch. Same Walk part (~8°). Scale with the weave
    // (toward the bill), not a hold — look-up stays shut (not a
    // call). Preen nibble stays preen. Hop-turn owns
    // sitTurnGapeAmp. Scratch owns sitScratchGapeAmp. Wattle hang
    // is independently gated (`sitLookWattleAmp`). Comb nod is
    // independently gated (`sitLookCombAmp`). Alarm
    // glance skip. No Find.
    // 0 = old shut bill (sitLookGapeAmp 0).
    private float SitLookGape01()
    {
        if (sitLookGapeAmp < 0.01f || mode != BirdMode.Sit)
            return 0f;
        if (energyRest)
            return 0f;
        if (idleLook >= -0.01f)
            return 0f;
        if (NightRoost() > 0.35f)
            return 0f;
        if (LandSettle01() > 0.01f)
            return 0f;
        if (SitTurnHop() > 0.01f)
            return 0f;
        if (idlePreen > 0.01f)
            return 0f;
        if (idleHop > 0.01f)
            return 0f;
        if (AlarmLook() > 0.01f)
            return 0f;
        return Mathf.Clamp(-idleLook, 0f, 1f)
            * Mathf.Clamp(sitLookGapeAmp, 0f, 1.5f);
    }

    // Sit forage scratch: part the same bill on the kick. Picture
    // only — Walk / Peck / Sit-preen / Attack keep their own gape.
    // Cry keeps Alarm. Wander already wrote idleHop; foot, lid,
    // pupil, and wing were live, Sit look / preen gape stayed
    // skipped, so scratch still flashed a shut bill on the kick.
    // Same Walk part (~8°). Pulse with idleHop (toward the foot),
    // not a hold — not a call. Preen nibble stays preen. Look-down
    // owns sitLookGapeAmp. Hop-turn owns sitTurnGapeAmp. Night
    // roost and land ruffle skip. Wattle hang is independently
    // gated (`sitScratchWattleAmp`). Comb nod is independently
    // gated (`sitScratchCombAmp`). No Find.
    // 0 = old shut bill (sitScratchGapeAmp 0).
    private float SitScratchGape01()
    {
        if (sitScratchGapeAmp < 0.01f || sitScratchAmp < 0.01f
            || mode != BirdMode.Sit)
            return 0f;
        if (energyRest)
            return 0f;
        if (idleHop < 0.01f)
            return 0f;
        if (NightRoost() > 0.35f)
            return 0f;
        if (LandSettle01() > 0.01f)
            return 0f;
        if (SitTurnHop() > 0.01f)
            return 0f;
        if (AlarmLookSit01() > 0.01f)
            return 0f;
        return idleHop * Mathf.Clamp(sitScratchGapeAmp, 0f, 1.5f);
    }

    // Night Sit: DayNightCycle already stretches sitMin. Picture of that
    // rest — beak under the near wing. Static Night01, no Find. 0 in
    // scenes without a clock (Bird / Chicken stay awake).
    private float NightRoost()
    {
        if (nightRoostAmp < 0.01f || mode != BirdMode.Sit)
            return 0f;
        float n = DayNightCycle.Night01;
        if (n < 0.40f)
            return 0f;
        return nightRoostAmp * Mathf.InverseLerp(0.40f, 0.72f, n);
    }

    private bool OnDirtSupport()
    {
        return sensors != null
            && (sensors.leftFootGrounded || sensors.rightFootGrounded);
    }

    // Glide inside the flare window, still in the air. Perch AABB
    // includes soil, so height still gates the drop. Cached sensors.
    private bool WantsLandingGear()
    {
        if (landGearAmp < 0.01f || mode != BirdMode.Glide)
            return false;
        if (sensors == null || bodyRb == null)
            return false;
        if (sensors.leftFootGrounded || sensors.rightFootGrounded)
            return false;
        float h = bodyRb.position.y - Bird.StanceRootY(bird);
        float flare = flight != null ? Mathf.Max(0.4f, flight.flareHeight) : 1.8f;
        return h < flare;
    }

    // Height inside flareHeight, 0…1. Same envelope as BirdFlight.flareLook
    // (one step late on ragdoll). Cached body, no Find.
    private float LandingFlare01()
    {
        if (bodyRb == null)
            return 0f;
        float h = bodyRb.position.y - Bird.StanceRootY(bird);
        float flare = flight != null ? Mathf.Max(0.08f, flight.flareHeight) : 1.8f;
        return 1f - Mathf.Clamp01(h / flare);
    }

    // Glide flare: raise the wing and cup the ulna. Picture only —
    // DriveWings still holds ragdoll pose. Lift stays in BirdFlight. No Find.
    // false = leave the held-glide / dirt flap mid (landWingAmp 0).
    private bool ApplyLandingWingFlare(ref float shoulder, ref float elbow)
    {
        if (landWingAmp < 0.01f || mode != BirdMode.Glide)
            return false;
        if (sensors != null && (sensors.leftFootGrounded || sensors.rightFootGrounded))
            return false;
        float t = LandingFlare01();
        if (t < 0.02f)
            return false;
        float a = Mathf.Clamp(landWingAmp, 0f, 1.5f) * t;
        // High jointAngle is wing-up (limits 25…165). Dirt still beats;
        // pad Glide cups so the surface is a brake, not a fold.
        shoulder = Mathf.Min(160f, shoulder + 24f * a);
        if (!WantsDirtLandingFlap())
            elbow = Mathf.Lerp(elbow, -32f, Mathf.Clamp01(a));
        return true;
    }

    // Brain already wrote Sit after Fly/Glide/Attack. The cup used to
    // snap to a fold; this is two opposite-wing shakes, then Sit.
    // Picture only — DriveWings still holds ragdoll pose. No Find.
    // Pupil drop is the same window (landSettleEyeAmp is its own lever).
    // Lid slit is the same window (landSettleLidAmp is its own lever).
    // Gape part is the same window (landSettleGapeAmp is its own lever).
    // Tail hitch is the same window (landSettleTailAmp is its own lever).
    private void NoteLandSettle()
    {
        bool fromAir = poseMode == BirdMode.Fly
            || poseMode == BirdMode.Glide
            || poseMode == BirdMode.Attack;
        if (landSettleAmp > 0.01f && mode == BirdMode.Sit && fromAir)
        {
            settleBorn = Time.time;
            settleUntil = settleBorn + 0.55f;
        }
        poseMode = mode;
    }

    private float LandSettle01()
    {
        if (landSettleAmp < 0.01f || mode != BirdMode.Sit)
            return 0f;
        if (Time.time >= settleUntil)
            return 0f;
        if (NightRoost() > 0.35f)
            return 0f;
        float dur = settleUntil - settleBorn;
        if (dur < 0.05f)
            return 0f;
        return Mathf.Clamp(landSettleAmp, 0f, 1.5f)
            * (1f - Mathf.Clamp01((Time.time - settleBorn) / dur));
    }

    private float LandSettleShake()
    {
        return Mathf.Sin((Time.time - settleBorn) * 22f);
    }

    // Flock Sit ruffle. Opposite wings so it is not preen (one wing to beak).
    // false = leave the snap fold (landSettleAmp 0 or the window is over).
    private bool ApplyLandSettlePicture()
    {
        float a = LandSettle01();
        if (a < 0.01f)
            return false;
        float s = LandSettleShake();
        flockLeftWingFold = flockWingFold + 22f * a * s;
        flockRightWingFold = flockWingFold - 18f * a * s;
        flockLeftUlnaZ = flockUlnaZ - 10f * a * Mathf.Max(0f, s);
        flockRightUlnaZ = flockUlnaZ - 10f * a * Mathf.Max(0f, -s);
        flockNeckZ = 6f * a * s;
        flockHeadZ = flockNeckZ * 0.45f;
        return true;
    }

    // Sit/Peck on a pad. Dirt Sit keeps a forward slab; without this
    // the twig Sit matches soil, so BIRD-5 rest reads as dirt. Toes
    // wrap the pad (negative Z drops the free end). Hop-turn and
    // scratch overwrite the moving foot after this. Alarm startle
    // grasp is independently gated (`alarmLookSitGraspAmp`).
    // Alarm startle tail is independently gated
    // (`alarmLookSitTailAmp`). Alarm startle blink is
    // independently gated (`alarmLookSitBlinkAmp`).
    // Picture only — DriveLegs still holds ragdoll pose. No Find.
    // 0 = old foot 0.
    private void ApplySitGraspPicture()
    {
        if (sitGraspAmp < 0.01f)
            return;
        if (perchPad == null)
            return;
        if (mode != BirdMode.Sit && mode != BirdMode.Peck)
            return;
        float a = Mathf.Clamp(sitGraspAmp, 0f, 1.5f);
        // B-PERCH-18: THE TOES LIE ON THE WOOD. The old -22/-20 left the foot's
        // own plane at -(sitHip 28 + crouch 10) - (shank 12 + crouch 18) - 22 =
        // -90 deg: the 0.05 m foot sprite rendered as a VERTICAL bar whose tip
        // poked into the limb (take bPCH17a, slot 11: a 10 x 14 px tan slab with
        // the wood's top edge 20-25 px above its bottom - "the feet sink inside
        // the brown branch fill", the phase-6 standing fault). +30/+28 puts the
        // world angle at -38/-40 deg: the toes' tip keeps the contact (the box's
        // lowest corner ~0.003 m above the perch volume's bottom at the same
        // seat) and the foot reads as a foot on the branch, not a bar in it.
        // Picture only - DriveLegs still holds the ragdoll pose. The lever is
        // the round's ONE change (bPCH18a vs bPCH17a, the same cut, the same eye).
        flockLeftFootZ = 30f * a;
        flockRightFootZ = 28f * a;
    }

    // Sit/Peck on a pad. Toes already wrap; thighs stayed the dirt
    // sitHip, so BIRD-5 rest still read as standing on a slab.
    // Extra tarsus crouch. Hop-turn and scratch overwrite the
    // moving leg after this. Alarm startle tail is independently
    // gated (`alarmLookSitTailAmp`). Picture only — DriveLegs
    // still holds ragdoll pose. No Find. 0 = old dirt-leg sit
    // on the twig.
    private void ApplySitCrouchPicture()
    {
        float u = SitCrouch01();
        if (u < 0.01f)
            return;
        flockLeftThighZ -= 10f * u;
        flockRightThighZ -= 8f * u;
        flockLeftShankZ -= 18f * u;
        flockRightShankZ -= 16f * u;
    }

    // 0 on dirt or when the lever is off. Peck is milder so the
    // forage jab is not a full Sit squat. Includes sitCrouchAmp.
    private float SitCrouch01()
    {
        if (sitCrouchAmp < 0.01f)
            return 0f;
        if (perchPad == null)
            return 0f;
        if (mode != BirdMode.Sit && mode != BirdMode.Peck)
            return 0f;
        float a = Mathf.Clamp(sitCrouchAmp, 0f, 1.5f);
        return mode == BirdMode.Peck ? 0.55f * a : a;
    }

    // Sit/Peck on a pad. Wrap and crouch already plant the feet; the
    // neck and wings stayed the dirt Sit, so BIRD-5 rest still read
    // as a hen standing on a slab. Retract the neck and tuck the
    // wings along the flanks. Land ruffle / preen / roost overwrite
    // after this. Peck skips the neck (the jab owns the dip).
    // Alarm startle hunch is independently gated
    // (`alarmLookSitHunchAmp`). Alarm startle tail is
    // independently gated (`alarmLookSitTailAmp`). Picture
    // only — DriveWings / DriveNeck still hold ragdoll pose.
    // No Find.
    // 0 = old dirt-sit silhouette on the twig.
    private void ApplySitHunchPicture()
    {
        float u = SitHunch01();
        if (u < 0.01f)
            return;
        flockLeftUlnaZ -= 14f * u;
        flockRightUlnaZ -= 12f * u;
        flockLeftWingFold -= 8f * u;
        flockRightWingFold -= 6f * u;
        if (mode != BirdMode.Sit)
            return;
        flockNeckZ = 12f * u;
        flockHeadZ = 7f * u;
    }

    // 0 on dirt or when the lever is off. Peck is milder so the
    // forage jab is not a full Sit hunch. Includes sitHunchAmp.
    private float SitHunch01()
    {
        if (sitHunchAmp < 0.01f)
            return 0f;
        if (perchPad == null)
            return 0f;
        if (mode != BirdMode.Sit && mode != BirdMode.Peck)
            return 0f;
        float a = Mathf.Clamp(sitHunchAmp, 0f, 1.5f);
        return mode == BirdMode.Peck ? 0.40f * a : a;
    }

    // Sit forage scratch. Brain already wrote idleHop (~0.3 s Abs-sin);
    // both legs used to crouch, so look/preen had a twitch twin.
    // Near (right) foot to the head. Far leg stays the Sit plant.
    // Picture only — DriveLegs still holds ragdoll pose. No Find.
    // false = leave the old both-leg bob (sitScratchAmp 0).
    private bool ApplySitScratchPicture()
    {
        if (sitScratchAmp < 0.01f || mode != BirdMode.Sit)
            return false;
        if (idleHop < 0.01f)
            return false;
        if (AlarmLookSit01() > 0.01f)
            return false;
        float a = Mathf.Clamp(sitScratchAmp, 0f, 1.5f) * idleHop;
        float kick = Mathf.Sin(Time.time * 22f);
        flockRightThighZ = 42f * a;
        flockRightShankZ = -58f * a - 10f * a * Mathf.Max(0f, kick);
        flockRightFootZ = 16f * a * kick;
        flockNeckZ = 12f * a;
        flockHeadZ = 8f * a;
        ApplySitScratchWingPicture();
        return true;
    }

    // Sit forage scratch: flick the far (left) wing off the Sit fold.
    // Picture only — DriveWings still holds ragdoll pose. Lift stays
    // in BirdFlight. Wander already wrote idleHop; the foot, slit,
    // and pupil were live; both wings stayed tucked, so scratch still
    // read as a twitching sit. Plus from −90 lifts the box (hop shrug),
    // not preen (more-negative, near wing to the beak). Near stays
    // tucked so the kicking foot has room. Pulse with the kick.
    // Night roost, land ruffle, and hop-turn skip. No Find.
    // 0 = old Sit fold (sitScratchWingAmp 0).
    private void ApplySitScratchWingPicture()
    {
        float u = SitScratchWing01();
        if (u < 0.01f)
            return;
        float kick = Mathf.Max(0f, Mathf.Sin(Time.time * 22f));
        float flick = u * (0.70f + 0.30f * kick);
        flockLeftWingFold += 22f * flick;
        flockLeftUlnaZ = Mathf.Lerp(flockLeftUlnaZ, groundFoldUlnaZ + groundFlickLiftDeg, Mathf.Clamp01(flick));
    }

    // 0 roost / ruffle / hop-turn / off, peak with idleHop.
    // Includes sitScratchWingAmp.
    private float SitScratchWing01()
    {
        if (sitScratchWingAmp < 0.01f || sitScratchAmp < 0.01f
            || mode != BirdMode.Sit)
            return 0f;
        if (energyRest)
            return 0f;
        if (idleHop < 0.01f)
            return 0f;
        if (NightRoost() > 0.35f)
            return 0f;
        if (LandSettle01() > 0.01f)
            return 0f;
        if (SitTurnHop() > 0.01f)
            return 0f;
        if (AlarmLookSit01() > 0.01f)
            return 0f;
        return idleHop * Mathf.Clamp(sitScratchWingAmp, 0f, 1.5f);
    }

    // Sit forage scratch: hitch the tail with the kick. Picture only —
    // DriveTail still holds ragdoll pitch. Look owns sitLookTailAmp.
    // Preen owns sitPreenTailAmp. Hop-turn owns sitTurnTailAmp.
    // Sit ruffle owns landSettleTailAmp.
    // Pad crouch / Alarm keep their own fans. Walk stride and hen
    // hop stay Walk. Wander
    // already wrote idleHop; foot, lid, pupil, wing, and bill were
    // live, Sit drop 6° (and the old slow 14° envelope on
    // sitScratchAmp) stayed put, so scratch still read as a kicking
    // sit. Pulse with idleHop, hitch with the same 22 Hz rake as
    // the foot. Night roost, land ruffle, and hop-turn skip. No
    // Find. 0 = old Sit drop (sitScratchTailAmp 0).
    private float SitScratchTail01()
    {
        if (sitScratchTailAmp < 0.01f || sitScratchAmp < 0.01f
            || mode != BirdMode.Sit)
            return 0f;
        if (energyRest)
            return 0f;
        if (idleHop < 0.01f)
            return 0f;
        if (NightRoost() > 0.35f)
            return 0f;
        if (LandSettle01() > 0.01f)
            return 0f;
        if (SitTurnHop() > 0.01f)
            return 0f;
        if (AlarmLookSit01() > 0.01f)
            return 0f;
        return idleHop * Mathf.Clamp(sitScratchTailAmp, 0f, 1.5f);
    }

    // Brain already wrote SetFacing (Wander Sit look-around). Look
    // scale flipped at once, so forage turn read as a texture swap.
    // Hop both feet then plant. Picture only — DriveLegs still holds
    // ragdoll pose. No Find. 0 = leave the instant flip.
    // Night roost and land ruffle skip (NoteSitTurn). Walk keeps stride.
    private void NoteSitTurn()
    {
        if (sitTurnAmp < 0.01f)
            return;
        if (mode != BirdMode.Sit)
            return;
        if (energyRest)
            return;
        if (NightRoost() > 0.35f)
            return;
        if (LandSettle01() > 0.01f)
            return;
        float now = Time.time;
        turnBorn = now;
        turnUntil = now + 0.38f;
    }

    // 0 at plant edges, peak mid-hop. Includes sitTurnAmp.
    private float SitTurnHop()
    {
        if (sitTurnAmp < 0.01f || mode != BirdMode.Sit)
            return 0f;
        if (NightRoost() > 0.35f)
            return 0f;
        if (LandSettle01() > 0.01f)
            return 0f;
        if (Time.time >= turnUntil)
            return 0f;
        float dur = turnUntil - turnBorn;
        if (dur < 0.05f)
            return 0f;
        float p = Mathf.Clamp01((Time.time - turnBorn) / dur);
        return Mathf.Clamp(sitTurnAmp, 0f, 1.5f) * Mathf.Sin(p * Mathf.PI);
    }

    private bool ApplySitTurnPicture()
    {
        float hop = SitTurnHop();
        if (hop < 0.01f)
            return false;
        flockLeftThighZ = -sitHipAngle + 22f * hop;
        flockRightThighZ = -sitHipAngle + 16f * hop;
        flockLeftShankZ = -12f - 18f * hop;
        flockRightShankZ = -12f - 14f * hop;
        flockLeftFootZ = 10f * hop;
        flockRightFootZ = 8f * hop;
        flockNeckZ = 14f * hop;
        flockHeadZ = flockNeckZ * 0.5f;
        ApplySitTurnWingPicture();
        return true;
    }

    // Sit hop-turn: shrug both wings off the Sit fold. Picture only —
    // DriveWings still holds ragdoll pose. Lift stays in BirdFlight.
    // Wander already wrote SetFacing; legs, neck, and tail hop, Sit
    // fold stayed put, so the about-face still read as a crouch
    // twitch. Plus from −90 (hen hop shrug), not preen. Scratch
    // owns the kick flick. Look / preen own the idle hitch. Walk
    // hop-turn owns walkTurnWingAmp. Sit hop-turn owns
    // sitTurnEyeAmp / sitTurnLidAmp / sitTurnGapeAmp /
    // sitTurnTailAmp. Night roost and land ruffle skip.
    // No Find. 0 = old Sit fold (sitTurnWingAmp 0).
    private void ApplySitTurnWingPicture()
    {
        float flick = SitTurnWing01();
        if (flick < 0.01f)
            return;
        flockLeftWingFold = flockWingFold + 20f * flick;
        flockRightWingFold = flockWingFold + 16f * flick;
        flockLeftUlnaZ = Mathf.Lerp(flockUlnaZ, groundFoldUlnaZ + groundFlickLiftDeg + 2f, Mathf.Clamp01(flick));
        flockRightUlnaZ = Mathf.Lerp(flockUlnaZ, groundFoldUlnaZ + groundFlickLiftDeg, Mathf.Clamp01(flick));
    }

    // 0 roost / ruffle / off, peak mid-hop. Includes sitTurnWingAmp.
    private float SitTurnWing01()
    {
        if (sitTurnWingAmp < 0.01f)
            return 0f;
        float hop = SitTurnHop();
        if (hop < 0.01f)
            return 0f;
        return hop * Mathf.Clamp(sitTurnWingAmp, 0f, 1.5f);
    }

    // Sit hop-turn: hitch the tail off the Sit drop. Picture only —
    // DriveTail still holds ragdoll pitch. Wander already wrote
    // SetFacing; legs, neck, wings, pupil, lid, and bill hop, the
    // fan still rode sitTurnAmp, so the about-face sat on the hop
    // envelope. Independently gated. Hold through the plant, not
    // a pulse — same sin as the hop. Look owns sitLookTailAmp.
    // Preen owns sitPreenTailAmp. Scratch owns sitScratchTailAmp.
    // Sit ruffle owns landSettleTailAmp. Walk hop-turn owns
    // walkTurnTailAmp. Air turn owns
    // airTurnTailAmp. Pad crouch / Alarm keep their own fans.
    // Night roost skip (LandSettle01).
    // No Find. 0 = old Sit drop (sitTurnTailAmp 0).
    private float SitTurnTail01()
    {
        if (sitTurnTailAmp < 0.01f)
            return 0f;
        float hop = SitTurnHop();
        if (hop < 0.01f)
            return 0f;
        return hop * Mathf.Clamp(sitTurnTailAmp, 0f, 1.5f);
    }

    // Brain already wrote SetFacing (Wander Walk about-face). Look
    // scale flipped at once while the gait kept going, so forage turn
    // read as a texture swap. Plant both feet then resume. Picture
    // only — walkThrust waits out the hop. No Find. 0 = instant flip.
    // Sit hop-turn stays on the perch (sitTurnAmp). Peck is not Walk.
    private void NoteWalkTurn()
    {
        if (walkTurnAmp < 0.01f)
            return;
        if (mode != BirdMode.Walk)
            return;
        if (energyRest)
            return;
        float now = Time.time;
        walkTurnBorn = now;
        walkTurnUntil = now + 0.32f;
    }

    // 0 at plant edges, peak mid-hop. Includes walkTurnAmp.
    private float WalkTurnHop()
    {
        if (walkTurnAmp < 0.01f || mode != BirdMode.Walk)
            return 0f;
        if (Time.time >= walkTurnUntil)
            return 0f;
        float dur = walkTurnUntil - walkTurnBorn;
        if (dur < 0.05f)
            return 0f;
        float p = Mathf.Clamp01((Time.time - walkTurnBorn) / dur);
        return Mathf.Clamp(walkTurnAmp, 0f, 1.5f) * Mathf.Sin(p * Mathf.PI);
    }

    private bool ApplyWalkTurnPicture()
    {
        float hop = WalkTurnHop();
        if (hop < 0.01f)
            return false;
        flockLeftThighZ = -walkHipAmp * 0.55f * hop;
        flockRightThighZ = -walkHipAmp * 0.45f * hop;
        flockLeftShankZ = -walkKneeAmp * 0.70f * hop;
        flockRightShankZ = -walkKneeAmp * 0.60f * hop;
        if (walkFootAmp > 0.01f)
        {
            flockLeftFootZ = -(flockLeftThighZ + flockLeftShankZ);
            flockRightFootZ = -(flockRightThighZ + flockRightShankZ);
        }
        else
            flockLeftFootZ = flockRightFootZ = 0f;
        flockNeckZ = 12f * hop;
        flockHeadZ = flockNeckZ * 0.5f;
        ApplyWalkTurnWingPicture();
        return true;
    }

    // Walk hop-turn: shrug both wings off the Walk fold. Picture only —
    // DriveWings still holds ragdoll pose. Lift stays in BirdFlight.
    // Wander already wrote SetFacing; legs, neck, and tail hop, Walk
    // fold stayed put (stride hitch and hen hop skip the window), so
    // the about-face still read as a crouch twitch. Plus from −90
    // (hen hop shrug), not preen. Sit hop-turn stays on the perch.
    // Stride hitch and hen hop keep their gait. Air turn stays bank.
    // Walk hop-turn owns walkTurnEyeAmp / walkTurnLidAmp /
    // walkTurnGapeAmp / walkTurnTailAmp. Sit hop-turn owns
    // sitTurnGapeAmp on the perch. No Find.
    // 0 = old Walk fold (walkTurnWingAmp 0).
    private void ApplyWalkTurnWingPicture()
    {
        float flick = WalkTurnWing01();
        if (flick < 0.01f)
            return;
        flockLeftWingFold = flockWingFold + 20f * flick;
        flockRightWingFold = flockWingFold + 16f * flick;
        flockLeftUlnaZ = Mathf.Lerp(flockUlnaZ, groundFoldUlnaZ + groundFlickLiftDeg + 2f, Mathf.Clamp01(flick));
        flockRightUlnaZ = Mathf.Lerp(flockUlnaZ, groundFoldUlnaZ + groundFlickLiftDeg, Mathf.Clamp01(flick));
    }

    // 0 off / window over, peak mid-hop. Includes walkTurnWingAmp.
    private float WalkTurnWing01()
    {
        if (walkTurnWingAmp < 0.01f)
            return 0f;
        float hop = WalkTurnHop();
        if (hop < 0.01f)
            return 0f;
        return hop * Mathf.Clamp(walkTurnWingAmp, 0f, 1.5f);
    }

    // Walk hop-turn: hitch the tail off the Sit drop. Picture only —
    // DriveTail still holds ragdoll pitch. Wander already wrote
    // SetFacing; legs, neck, wings, pupil, lid, and bill hop, the
    // fan still rode walkTurnAmp, so the about-face sat on the hop
    // envelope. Independently gated. Hold through the plant, not
    // a pulse — same sin as the hop. Sit hop-turn owns
    // sitTurnTailAmp. Air turn owns airTurnTailAmp. Stride owns
    // walkStrideTailAmp. Hen hop owns walkHopTailAmp. Pad crouch
    // / Alarm keep their own fans.
    // No Find. 0 = old Sit drop (walkTurnTailAmp 0).
    private float WalkTurnTail01()
    {
        if (walkTurnTailAmp < 0.01f)
            return 0f;
        float hop = WalkTurnHop();
        if (hop < 0.01f)
            return 0f;
        return hop * Mathf.Clamp(walkTurnTailAmp, 0f, 1.5f);
    }

    // Brain already wrote SetFacing (Wander Fly/Glide about-face). Look
    // scale flipped at once while the stroke stayed cruise, so the
    // turn read as a texture swap. Bank the span then resume. Picture
    // only — BirdFlight still banks lookRoot and softens pitch. No Find.
    // 0 = instant flip. Sit/Walk keep their hop-turns. Attack/dirt
    // takeoff/landing flare skip. Do not extend on Glide homeX chatter.
    private void NoteAirTurn()
    {
        if (airTurnAmp < 0.01f)
            return;
        if (mode != BirdMode.Fly && mode != BirdMode.Glide)
            return;
        if (energyRest)
            return;
        if (OnDirtSupport())
            return;
        if (WantsLandingGear())
            return;
        if (TakeoffWing01() > 0.01f || TakeoffTrail01() > 0.01f)
            return;
        float now = Time.time;
        if (now < airTurnUntil)
            return;
        airTurnBorn = now;
        airTurnUntil = now + 0.40f;
    }

    // 0 at the flip edges, peak mid-bank. Includes airTurnAmp.
    private float AirTurn01()
    {
        if (airTurnAmp < 0.01f)
            return 0f;
        if (mode != BirdMode.Fly && mode != BirdMode.Glide)
            return 0f;
        if (OnDirtSupport())
            return 0f;
        if (WantsLandingGear())
            return 0f;
        if (TakeoffWing01() > 0.01f || TakeoffTrail01() > 0.01f)
            return 0f;
        if (Time.time >= airTurnUntil)
            return 0f;
        float dur = airTurnUntil - airTurnBorn;
        if (dur < 0.05f)
            return 0f;
        float p = Mathf.Clamp01((Time.time - airTurnBorn) / dur);
        return Mathf.Clamp(airTurnAmp, 0f, 1.5f) * Mathf.Sin(p * Mathf.PI);
    }

    // Far wing up, near wing down. Fold −90 is along the back; a
    // plus on Fly/Glide raises the free end (not Sit preen). false =
    // leave the cruise stroke (airTurnAmp 0 or the window is over).
    private bool ApplyAirTurnPicture()
    {
        float a = AirTurn01();
        if (a < 0.01f)
            return false;
        flockLeftWingFold = flockWingFold + 16f * a;
        flockRightWingFold = flockWingFold - 14f * a;
        flockLeftUlnaZ = flockUlnaZ - 8f * a;
        flockRightUlnaZ = flockUlnaZ + 6f * a;
        flockNeckZ = 10f * a;
        flockHeadZ = flockNeckZ * 0.5f;
        return true;
    }

    // Fly/Glide SetFacing: hitch the tail off the cruise stream.
    // Picture only — DriveTail still holds ragdoll pitch. Wander
    // already wrote SetFacing; wings and neck bank, the fan still
    // rode airTurnAmp, so the about-face sat on the bank envelope.
    // Independently gated. Hold through the bank, not a pulse —
    // same sin as the bank. Sit hop-turn owns sitTurnTailAmp.
    // Walk hop-turn owns walkTurnTailAmp. Takeoff / landing
    // flare / Alarm keep their own fans. Dirt takeoff, pad
    // first-air trail, landing flare, and Attack skip with
    // AirTurn01. Pupil drop is independently gated
    // (airTurnEyeAmp). Lid slit is independently gated
    // (airTurnLidAmp). Gape is independently gated
    // (airTurnGapeAmp). No Find. 0 = old cruise stream
    // (airTurnTailAmp 0).
    private float AirTurnTail01()
    {
        if (airTurnTailAmp < 0.01f)
            return 0f;
        float air = AirTurn01();
        if (air < 0.01f)
            return 0f;
        return air * Mathf.Clamp(airTurnTailAmp, 0f, 1.5f);
    }

    // Ragdoll Sit ruffle. Same opposite-wing picture. false = preen/roost.
    private bool ApplyLandSettleWings(ref float leftShoulder, ref float rightShoulder,
        ref float leftElbow, ref float rightElbow)
    {
        float a = LandSettle01();
        if (a < 0.01f)
            return false;
        float s = LandSettleShake();
        leftShoulder = flapMidAngle + 16f * a * s;
        rightShoulder = flapMidAngle - 12f * a * s;
        leftElbow = Mathf.Lerp(-62f, -78f, Mathf.Clamp01(a * Mathf.Max(0f, s)));
        rightElbow = Mathf.Lerp(-62f, -78f, Mathf.Clamp01(a * Mathf.Max(0f, -s)));
        return true;
    }

    // Glide flare: hang the shanks and plant the boxes. Picture only —
    // DriveLegs still holds ragdoll pose. Lift stays in BirdFlight. No Find.
    // false = leave the dirt stick-hang / perch tuck (landGearAmp 0).
    private bool ApplyLandingGearPicture()
    {
        if (!WantsLandingGear())
            return false;
        float a = Mathf.Clamp(landGearAmp, 0f, 1.5f);
        flockLeftThighZ = flockRightThighZ = -10f * a;
        flockLeftShankZ = flockRightShankZ = -8f * a;
        // Cancel parent so the foot box sits ready — not stoop toes, not tuck.
        flockLeftFootZ = flockRightFootZ = -(flockLeftThighZ + flockLeftShankZ);
        flockNeckZ = 8f * a;
        flockHeadZ = 4f * a;
        return true;
    }

    // Hen Walk: both legs coil, then tuck. Picture only —
    // DriveLegs still holds ragdoll pose. walkThrust still translates.
    // false = leave the crow stride (walkHopAmp 0).
    private bool ApplyWalkHopPicture()
    {
        if (walkHopAmp < 0.01f || mode != BirdMode.Walk)
            return false;
        if (AlarmLookLeg01() > 0.01f)
            return false;
        float a = Mathf.Clamp(walkHopAmp, 0f, 1.5f);
        float u = WalkHopDown01();
        flockLeftThighZ = flockRightThighZ = -walkHipAmp * a * u;
        flockLeftShankZ = flockRightShankZ = -walkKneeAmp * a * (1f - u);
        if (walkFootAmp > 0.01f)
        {
            float plant = -(flockLeftThighZ + flockLeftShankZ);
            float air = -walkFootAmp * a * (1f - u);
            flockLeftFootZ = flockRightFootZ = plant * u + air;
        }
        else
            flockLeftFootZ = flockRightFootZ = 0f;
        // Land dips the beak; crow walk nodded on one stride instead.
        // Brain already wrote Walk. Tuck already looks down; plant
        // sat at 0 so the bounce still read as a commute. Hold a
        // dirt scan through the plant.
        flockNeckZ = -walkHeadBob * a * u - WalkScanDip();
        flockHeadZ = flockNeckZ * 0.45f;
        // Brain already wrote Walk. Legs coiled; the Sit-adjacent
        // fold made the hop read as a crouch twitch. Both wings
        // shrug on the tuck, then fold on plant.
        ApplyWalkHopWingPicture();
        return true;
    }

    // Ragdoll hen Walk: same coil, both hips. false = crow stride.
    private bool ApplyWalkHopLegs(ref float leftHip, ref float rightHip,
        ref float leftKnee, ref float rightKnee)
    {
        if (walkHopAmp < 0.01f || mode != BirdMode.Walk)
            return false;
        if (AlarmLookLeg01() > 0.01f)
            return false;
        float a = Mathf.Clamp(walkHopAmp, 0f, 1.5f);
        float u = WalkHopDown01();
        leftHip = rightHip = standHipAngle + walkHipAmp * a * u;
        leftKnee = rightKnee = standKneeAngle + walkKneeAmp * a * (1f - u);
        return true;
    }

    // Crow dirt Search: crouch the tarsi and scan the soil. Picture
    // only — DriveLegs still holds ragdoll pose. walkThrust still
    // translates. Hens keep walkHop. Peck owns the jab. No Find.
    // false = leave the crow stride (searchCrouch 0).
    private bool ApplySearchCrouchPicture()
    {
        if (searchCrouch < 0.01f || mode != BirdMode.Walk)
            return false;
        if (sensors != null && !sensors.bothGrounded)
            return false;
        if (WalkTurnHop() > 0.01f)
            return false;
        if (AlarmLookLeg01() > 0.01f)
            return false;
        float a = Mathf.Clamp01(searchCrouch);
        float hip = sitHipAngle * (0.45f + 0.40f * a);
        flockLeftThighZ = -hip;
        flockRightThighZ = -hip * 0.92f;
        flockLeftShankZ = -12f - 10f * a;
        flockRightShankZ = -12f - 8f * a;
        flockLeftFootZ = flockRightFootZ = 0f;
        flockNeckZ = -22f * a + 6f * Mathf.Sin(Time.fixedTime * 3.2f);
        flockHeadZ = flockNeckZ * 0.4f;
        return true;
    }

    // Ragdoll crow Search: modest extra tuck. Flock look does the
    // crouch; a ragdoll squat would unplant. false = crow stride.
    private bool ApplySearchCrouchLegs(ref float leftHip, ref float rightHip,
        ref float leftKnee, ref float rightKnee)
    {
        if (searchCrouch < 0.01f || mode != BirdMode.Walk)
            return false;
        if (AlarmLookLeg01() > 0.01f)
            return false;
        float a = Mathf.Clamp01(searchCrouch);
        leftHip = rightHip = sitHipAngle * (0.55f + 0.25f * a);
        leftKnee = rightKnee = standKneeAngle + 14f * a;
        return true;
    }

    // 1 = planted coil, 0 = both legs tucked. Same clock as walkFrequency.
    private float WalkHopDown01()
    {
        float w = (Mathf.PI * 2f) * WalkGaitPhase();
        return 0.5f + 0.5f * Mathf.Sin(w);
    }

    // Hen Walk hop: both wings shrug on the tuck. Picture only —
    // DriveWings still holds ragdoll pose. Lift stays in BirdFlight.
    // No Find. 0 = old folded Walk (walkHopWingAmp 0 or crow stride).
    private void ApplyWalkHopWingPicture()
    {
        float flick = WalkHopWingFlick();
        if (flick < 0.01f)
            return;
        // Plus from −90 lifts the box off the back (takeoff cock),
        // not preen (more-negative, one wing to the beak).
        flockLeftWingFold = flockWingFold + 20f * flick;
        flockRightWingFold = flockWingFold + 16f * flick;
        flockLeftUlnaZ = Mathf.Lerp(flockUlnaZ, groundFoldUlnaZ + groundFlickLiftDeg + 2f, Mathf.Clamp01(flick));
        flockRightUlnaZ = Mathf.Lerp(flockUlnaZ, groundFoldUlnaZ + groundFlickLiftDeg, Mathf.Clamp01(flick));
    }

    // 0 plant / crow / off, peak mid-tuck. Includes walkHopWingAmp.
    private float WalkHopWingFlick()
    {
        if (walkHopWingAmp < 0.01f || walkHopAmp < 0.01f)
            return 0f;
        if (mode != BirdMode.Walk)
            return 0f;
        if (WalkTurnHop() > 0.01f)
            return 0f;
        if (AlarmLook() > 0.01f)
            return 0f;
        float a = Mathf.Clamp(walkHopWingAmp, 0f, 1.5f)
            * Mathf.Clamp(walkHopAmp, 0f, 1.5f);
        return a * (1f - WalkHopDown01());
    }

    // Crow Walk stride: opposite wing hitch on the swing thigh.
    // Picture only — DriveWings still holds ragdoll pose. Lift
    // stays in BirdFlight. Hen hop owns both wings. No Find.
    // 0 = old Sit-adjacent fold (walkStrideWingAmp 0 or hen hop).
    private void ApplyWalkStrideWingPicture()
    {
        if (!WalkStrideWingActive())
            return;
        float a = Mathf.Clamp(walkStrideWingAmp, 0f, 1.5f);
        float w = (Mathf.PI * 2f) * walkFrequency * Time.fixedTime;
        float sL = Mathf.Sin(w);
        float swingL = WalkSwing01(sL);
        float swingR = WalkSwing01(-sL);
        // Plus from −90 lifts the box off the back (hop shrug),
        // not preen (more-negative, one wing to the beak).
        flockLeftWingFold = flockWingFold + 12f * a * swingR;
        flockRightWingFold = flockWingFold + 10f * a * swingL;
        flockLeftUlnaZ = Mathf.Lerp(flockUlnaZ, groundFoldUlnaZ + groundFlickLiftDeg - 2f, Mathf.Clamp01(a * swingR));
        flockRightUlnaZ = Mathf.Lerp(flockUlnaZ, groundFoldUlnaZ + groundFlickLiftDeg, Mathf.Clamp01(a * swingL));
    }

    // Plus sin is the forward thigh, same as ragdoll hip/knee. Flock
    // hip visual is already minus; Max(0, −sin) folded the trail.
    private static float WalkSwing01(float hipSin)
    {
        return Mathf.Max(0f, hipSin);
    }

    private bool WalkStrideWingActive()
    {
        if (walkStrideWingAmp < 0.01f || walkHopAmp > 0.01f)
            return false;
        if (mode != BirdMode.Walk)
            return false;
        if (sensors != null && !sensors.bothGrounded && !OnHuntDirt())
            return false;
        if (WalkTurnHop() > 0.01f)
            return false;
        if (AlarmLook() > 0.01f)
            return false;
        return true;
    }

    // Crow Walk stride: drop the free end on each plant and swing.
    // Picture only — DriveTail still holds ragdoll pitch. Hen hop
    // owns walkHopTailAmp. Walk hop-turn owns walkTurnTailAmp. No Find.
    // 0 = old Sit drop (walkStrideTailAmp 0 or hen hop).
    private float WalkStrideTail01()
    {
        if (!WalkStrideTailActive())
            return 0f;
        float a = Mathf.Clamp(walkStrideTailAmp, 0f, 1.5f);
        float w = (Mathf.PI * 2f) * walkFrequency * Time.fixedTime;
        return a * Mathf.Abs(Mathf.Sin(w));
    }

    private bool WalkStrideTailActive()
    {
        if (walkStrideTailAmp < 0.01f || walkHopAmp > 0.01f)
            return false;
        if (mode != BirdMode.Walk)
            return false;
        if (sensors != null && !sensors.bothGrounded)
            return false;
        if (WalkTurnHop() > 0.01f)
            return false;
        if (AlarmLookLeg01() > 0.01f)
            return false;
        return true;
    }

    // Hen Walk hop: drop the free end on the tuck. Picture only —
    // DriveTail still holds ragdoll pitch. Crow stride owns
    // walkStrideTailAmp. Walk hop-turn owns walkTurnTailAmp. No Find.
    // 0 = old Sit drop (walkHopTailAmp 0 or crow stride).
    private float WalkHopTail01()
    {
        if (!WalkHopTailActive())
            return 0f;
        float a = Mathf.Clamp(walkHopTailAmp, 0f, 1.5f)
            * Mathf.Clamp(walkHopAmp, 0f, 1.5f);
        return a * (1f - WalkHopDown01());
    }

    private bool WalkHopTailActive()
    {
        if (walkHopTailAmp < 0.01f || walkHopAmp < 0.01f)
            return false;
        if (mode != BirdMode.Walk)
            return false;
        if (sensors != null && !sensors.bothGrounded)
            return false;
        if (WalkTurnHop() > 0.01f)
            return false;
        if (AlarmLookLeg01() > 0.01f)
            return false;
        return true;
    }

    // Dirt Walk forage: hold the beak toward the soil. Picture only —
    // DriveNeck still holds ragdoll pose. Peck owns the jab dip.
    // Hop-turn and Alarm glance keep their own neck. No Find.
    // Degrees, plus = up. 0 = old ahead bob (walkScanAmp 0).
    private float WalkScanDip()
    {
        if (walkScanAmp < 0.01f || mode != BirdMode.Walk)
            return 0f;
        if (sensors != null && !sensors.bothGrounded)
            return 0f;
        if (WalkTurnHop() > 0.01f)
            return 0f;
        if (AlarmLook() > 0.01f)
            return 0f;
        return 10f * Mathf.Clamp(walkScanAmp, 0f, 1.5f);
    }

    // Brain already wrote Fly. Dirt used to coil then vanish into a
    // belly-tuck; this trails the feet for one beat, then folds.
    // Picture only — DriveLegs still holds ragdoll pose. No Find.
    // Wing cock uses the same dirt-leave edge so trailAmp 0 still
    // allows the first stroke (takeoffWingAmp is its own lever).
    // Tail fan is the same edge (takeoffTailAmp is its own lever).
    // First-air pupil is the same edge (takeoffEyeAmp is its own lever).
    // First-air lid is the same edge (takeoffLidAmp is its own lever).
    // First-air gape is the same edge (takeoffGapeAmp is its own lever).
    // First-air wattle is the same edge (takeoffWattleAmp is its own lever).
    // First-air comb is the same edge (takeoffCombAmp is its own lever).
    // Pad Sit/Peck: TickFlock releases the pad on the first Fly step,
    // so OnDirtSupport never holds. Same first-air clocks as dirt leave.
    private void NoteTakeoffTrail()
    {
        // Leftover Fly on a hunt plate must not arm takeoff clocks.
        // A 6 cm probe flicker (wasDirtFly → air → Fly) started the
        // 0.48 s trail; ragdoll legs tucked and BeakStrike never
        // Rel Hostile. IsHuntSoil covers dirt Walk/Peck after flicker.
        // Wander Sit→Fly still uses the edge below.
        if (IsHuntSoil())
        {
            trailUntil = 0f;
            wingUntil = 0f;
            tailUntil = 0f;
            takeoffEyeUntil = 0f;
            takeoffLidUntil = 0f;
            takeoffGapeUntil = 0f;
            takeoffWattleUntil = 0f;
            takeoffCombUntil = 0f;
            wasDirtFly = false;
            wasPerchedRest = perchPad != null && !IsFlightMode();
            return;
        }

        bool dirtFly = mode == BirdMode.Fly && OnDirtSupport();
        if (wasDirtFly && !dirtFly && mode == BirdMode.Fly)
            StartTakeoffAirClocks();

        bool perchedRest = perchPad != null && !IsFlightMode();
        if (wasPerchedRest && mode == BirdMode.Fly && perchTakeoffAmp > 0.01f)
            StartTakeoffAirClocks();
        wasPerchedRest = perchedRest;

        if (mode != BirdMode.Fly)
        {
            trailUntil = 0f;
            wingUntil = 0f;
            tailUntil = 0f;
            takeoffEyeUntil = 0f;
            takeoffLidUntil = 0f;
            takeoffGapeUntil = 0f;
            takeoffWattleUntil = 0f;
            takeoffCombUntil = 0f;
        }
        wasDirtFly = dirtFly;
    }

    // First-air trail / stroke / tail / pupil / lid / gape / wattle / comb. No Find. A second
    // edge in the same window (pad Fly then a one-frame probe
    // flicker) must not restart the envelope.
    private void StartTakeoffAirClocks()
    {
        float now = Time.time;
        if (now < trailUntil || now < wingUntil || now < tailUntil
            || now < takeoffEyeUntil || now < takeoffLidUntil
            || now < takeoffGapeUntil || now < takeoffWattleUntil
            || now < takeoffCombUntil)
            return;
        // B-TAKEOFF-2: the takeoff burst is armed on this same edge — the first
        // strokes off the ground are the leap's wing, not the cruise's.
        ArmTakeoffBurst();
        if (takeoffTrailAmp > 0.01f)
        {
            trailBorn = now;
            trailUntil = now + 0.48f;
        }
        if (takeoffWingAmp > 0.01f)
        {
            wingBorn = now;
            wingUntil = now + 0.48f;
        }
        if (takeoffTailAmp > 0.01f)
        {
            tailBorn = now;
            tailUntil = now + 0.48f;
        }
        if (takeoffEyeAmp > 0.01f)
        {
            takeoffEyeBorn = now;
            takeoffEyeUntil = now + 0.48f;
        }
        if (takeoffLidAmp > 0.01f)
        {
            takeoffLidBorn = now;
            takeoffLidUntil = now + 0.48f;
        }
        if (takeoffGapeAmp > 0.01f)
        {
            takeoffGapeBorn = now;
            takeoffGapeUntil = now + 0.48f;
        }
        if (takeoffWattleAmp > 0.01f)
        {
            takeoffWattleBorn = now;
            takeoffWattleUntil = now + 0.48f;
        }
        if (takeoffCombAmp > 0.01f)
        {
            takeoffCombBorn = now;
            takeoffCombUntil = now + 0.48f;
        }
    }

    private float TakeoffTrail01()
    {
        if (takeoffTrailAmp < 0.01f || mode != BirdMode.Fly)
            return 0f;
        if (Time.time >= trailUntil)
            return 0f;
        float dur = trailUntil - trailBorn;
        if (dur < 0.05f)
            return 0f;
        return Mathf.Clamp(takeoffTrailAmp, 0f, 1.5f)
            * (1f - Mathf.Clamp01((Time.time - trailBorn) / dur));
    }

    // Fly just left dirt or a pad: hang then tuck. false = old snap
    // tuck (takeoffTrailAmp 0 or the window is over). Attack/Glide
    // skip because mode is not Fly (Note zeros the clock).
    private bool ApplyTakeoffTrailPicture()
    {
        float t = TakeoffTrail01();
        if (t < 0.01f)
            return false;
        float hang = Mathf.Clamp01(t);
        float u = 1f - hang;
        u *= u;
        float a = Mathf.Clamp(takeoffTrailAmp, 0f, 1.5f);
        flockLeftThighZ = flockRightThighZ = Mathf.Lerp(-6f * a, -tuckHipAngle, u);
        flockLeftShankZ = flockRightShankZ = Mathf.Lerp(-8f * a, -tuckKneeAngle, u);
        flockLeftFootZ = flockRightFootZ = 12f * a * hang;
        flockNeckZ = -6f * a * hang;
        flockHeadZ = flockNeckZ * 0.45f;
        return true;
    }

    // Fly still on dirt: raise the wing (cocked). First air: extra
    // downstroke that fades on its own clock. Picture only —
    // EvaluateFlap still clocks lift. DriveWings still holds ragdoll
    // pose. No Find. false = leave the cruise flap (takeoffWingAmp 0).
    // Attack/Glide skip.
    private bool ApplyTakeoffWingStroke(ref float shoulder, ref float elbow)
    {
        if (takeoffWingAmp < 0.01f || mode != BirdMode.Fly)
            return false;
        float a = Mathf.Clamp(takeoffWingAmp, 0f, 1.5f);
        if (OnDirtSupport())
        {
            // High jointAngle is wing-up. Dirt used to beat at cruise
            // height while the legs coiled, so takeoff read as a helicopter.
            shoulder = Mathf.Min(160f, shoulder + 26f * a);
            elbow = Mathf.Lerp(elbow, -6f, Mathf.Clamp01(a));
            return true;
        }
        float hang = TakeoffWing01();
        if (hang < 0.01f)
            return false;
        // First air: extra down. Trail already hangs the feet; without
        // this the first climb is the same stroke as cruise.
        shoulder = Mathf.Max(28f, shoulder - 22f * a * hang);
        elbow = Mathf.Lerp(elbow, -8f, hang);
        return true;
    }

    private float TakeoffWing01()
    {
        if (takeoffWingAmp < 0.01f || mode != BirdMode.Fly)
            return 0f;
        if (Time.time >= wingUntil)
            return 0f;
        float dur = wingUntil - wingBorn;
        if (dur < 0.05f)
            return 0f;
        return Mathf.Clamp(takeoffWingAmp, 0f, 1.5f)
            * (1f - Mathf.Clamp01((Time.time - wingBorn) / dur));
    }

    // Fly dirt/first air: drop the free end (same sign as landing
    // flare). Picture only — DriveTail still holds ragdoll pitch.
    // No Find. 0 = leave the cruise stream (takeoffTailAmp 0).
    // Attack/Glide skip because mode is not Fly (Note zeros the clock).
    private float TakeoffTailFan()
    {
        if (takeoffTailAmp < 0.01f || mode != BirdMode.Fly)
            return 0f;
        float a = Mathf.Clamp(takeoffTailAmp, 0f, 1.5f);
        if (OnDirtSupport())
            return a;
        return TakeoffTail01();
    }

    private float TakeoffTail01()
    {
        if (takeoffTailAmp < 0.01f || mode != BirdMode.Fly)
            return 0f;
        if (Time.time >= tailUntil)
            return 0f;
        float dur = tailUntil - tailBorn;
        if (dur < 0.05f)
            return 0f;
        return Mathf.Clamp(takeoffTailAmp, 0f, 1.5f)
            * (1f - Mathf.Clamp01((Time.time - tailBorn) / dur));
    }

    // Fly still touching dirt: coil the look-legs. Picture only —
    // DriveLegs still holds ragdoll pose. Lift stays in BirdFlight. No Find.
    // false = leave the stand (takeoffCrouchAmp 0).
    private bool ApplyTakeoffCrouchPicture()
    {
        if (takeoffCrouchAmp < 0.01f)
            return false;
        float a = Mathf.Clamp(takeoffCrouchAmp, 0f, 1.5f);
        flockLeftThighZ = flockRightThighZ = -sitHipAngle * a;
        flockLeftShankZ = flockRightShankZ = -16f * a;
        flockLeftFootZ = flockRightFootZ = 0f;
        flockNeckZ = -12f * a;
        flockHeadZ = -6f * a;
        return true;
    }

    // Attack stoop: hold the bill open. Picture only — BeakStrike still
    // reads the trigger. Peck keeps its Cos gape. Cry keeps Alarm.
    // DriveNeck still holds ragdoll pose. Tongue is independently
    // gated (`stoopTongueAmp`). Hen wattle is independently gated
    // (`stoopWattleAmp`). Gullet is independently gated
    // (`stoopCropAmp`). No Find.
    // 0 = old closed bill (stoopGapeAmp 0).
    private float StoopGape()
    {
        if (stoopGapeAmp < 0.01f || mode != BirdMode.Attack)
            return 0f;
        return Mathf.Clamp(stoopGapeAmp, 0f, 1.5f);
    }

    // BIRD-6 Flee: pin Fly / hitch Walk through the glance. Picture
    // only — DriveWings still holds ragdoll pose. Lift stays in
    // BirdFlight. EvaluateFlap still clocks lift. No Find.
    // Fold −90 is along the back; slight plus presses the Fly box
    // (stoop pin), not preen. Walk hitch is the hop shrug from the
    // fold. Sit startle hitch is independently gated
    // (`alarmLookSitWingAmp`). 0 = leave the cruise flap
    // (alarmLookWingAmp 0).
    private void ApplyAlarmLookWingPicture()
    {
        float u = AlarmLookWing01();
        if (u < 0.01f)
            return;
        if (mode == BirdMode.Fly)
        {
            flockWingFold = -90f + 12f * u;
            flockLeftWingFold = flockRightWingFold = flockWingFold;
            flockUlnaZ = Mathf.Lerp(flockUlnaZ, -50f, Mathf.Clamp01(u));
            flockLeftUlnaZ = flockRightUlnaZ = flockUlnaZ;
            return;
        }
        flockLeftWingFold = flockWingFold + 16f * u;
        flockRightWingFold = flockWingFold + 14f * u;
        flockLeftUlnaZ = Mathf.Lerp(flockUlnaZ, groundFoldUlnaZ + groundFlickLiftDeg - 6f, Mathf.Clamp01(u));
        flockRightUlnaZ = Mathf.Lerp(flockUlnaZ, groundFoldUlnaZ + groundFlickLiftDeg - 4f, Mathf.Clamp01(u));
    }

    // BIRD-6 Flee Sit: hitch both wings off the Sit fold through the
    // glance. Picture only — DriveWings still holds ragdoll pose.
    // Lift stays in BirdFlight. Freeze already killed look/preen/
    // scratch; Sit still tucked, so Flee still flashed a folded perch
    // after the 0.55 s cry on the 1.15 s glance. Plus from −90
    // (hop shrug / Walk hitch), not preen. Hop-turn keeps the
    // about-face. Land ruffle keeps the plant. Night roost skip.
    // Fly pin / Walk hitch stay theirs. No Find.
    // 0 = leave the Sit fold (alarmLookSitWingAmp 0).
    private void ApplyAlarmLookSitWingPicture()
    {
        float u = AlarmLookSitWing01();
        if (u < 0.01f)
            return;
        flockLeftWingFold = flockWingFold + 16f * u;
        flockRightWingFold = flockWingFold + 14f * u;
        flockLeftUlnaZ = Mathf.Lerp(flockUlnaZ, groundFoldUlnaZ + groundFlickLiftDeg - 6f, Mathf.Clamp01(u));
        flockRightUlnaZ = Mathf.Lerp(flockUlnaZ, groundFoldUlnaZ + groundFlickLiftDeg - 4f, Mathf.Clamp01(u));
    }

    // BIRD-6 Flee Sit: crouch both legs off the Sit plant through the
    // glance. Picture only — DriveLegs still holds ragdoll pose.
    // Freeze already killed look/preen/scratch; wings hitch in
    // ApplyCryPicture; Sit still planted, so Flee still flashed a
    // perched sit after the 0.55 s cry on the 1.15 s glance.
    // Extra minus from sitHip (pad crouch already ran). Hop-turn
    // keeps the about-face. Land ruffle keeps the plant. Night
    // roost skip. Walk scramble stays Walk. Sit startle grasp is
    // independently gated (`alarmLookSitGraspAmp`). Sit startle
    // hunch is independently gated (`alarmLookSitHunchAmp`). Sit
    // startle tail is independently gated (`alarmLookSitTailAmp`).
    // No Find. 0 = leave the Sit plant (alarmLookSitLegAmp 0).
    private void ApplyAlarmLookSitLegPicture()
    {
        float u = AlarmLookSitLeg01();
        if (u < 0.01f)
            return;
        flockLeftThighZ -= 12f * u;
        flockRightThighZ -= 10f * u;
        flockLeftShankZ -= 16f * u;
        flockRightShankZ -= 14f * u;
    }

    // BIRD-6 Flee Sit: clench both feet off the Sit plant through the
    // glance. Picture only — DriveLegs still holds ragdoll pose
    // (ankles stay CoM). Freeze already killed look/preen/scratch;
    // wings hitch in ApplyCryPicture; thighs crouch above; Sit still
    // planted the toes (pad wrap / dirt 0), so Flee still flashed a
    // perched sit after the 0.55 s cry on the 1.15 s glance.
    // Extra minus from the plant (pad wrap already ran). Hop-turn
    // keeps the about-face. Land ruffle keeps the plant. Night
    // roost skip. Walk scramble stays Walk. Scratch raise stays
    // forage. No Find.
    // 0 = leave the Sit plant feet (alarmLookSitGraspAmp 0).
    private void ApplyAlarmLookSitGraspPicture()
    {
        float u = AlarmLookSitGrasp01();
        if (u < 0.01f)
            return;
        flockLeftFootZ -= 12f * u;
        flockRightFootZ -= 10f * u;
    }

    // BIRD-6 Flee Sit: hunch neck and tuck wings off the Sit
    // silhouette through the glance. Picture only — DriveNeck /
    // DriveWings still hold ragdoll pose. Lift stays in BirdFlight.
    // Freeze already killed look/preen/scratch; hitch already
    // flashed the fold; thighs crouch and toes clench above; Sit
    // still held the dirt-sit silhouette (pad retract / dirt 0
    // extra), so Flee still flashed a perched sit after the 0.55 s
    // cry on the 1.15 s glance. Extra plus on neck / extra minus
    // on fold (pad hunch already ran; hitch already set). Hop-turn
    // keeps the about-face. Land ruffle keeps the plant. Night
    // roost skip. Walk scramble stays Walk. Pad hunch stays the
    // twig rest. Sit startle tail is independently gated
    // (`alarmLookSitTailAmp`). No Find.
    // 0 = leave the Sit silhouette (alarmLookSitHunchAmp 0).
    private void ApplyAlarmLookSitHunchPicture()
    {
        float u = AlarmLookSitHunch01();
        if (u < 0.01f)
            return;
        flockLeftUlnaZ -= 14f * u;
        flockRightUlnaZ -= 12f * u;
        flockLeftWingFold -= 8f * u;
        flockRightWingFold -= 6f * u;
        flockNeckZ += 12f * u;
        flockHeadZ += 7f * u;
    }

    // Ragdoll Fly: drop the shoulder toward the back and hold, not
    // a cruise stroke. false = leave the flap (alarmLookWingAmp 0
    // or Walk — Walk hitch is DriveWings after the fold).
    private bool ApplyAlarmLookWingPin(ref float shoulder, ref float elbow, ref float wrist)
    {
        float u = AlarmLookWing01();
        if (u < 0.01f || mode != BirdMode.Fly)
            return false;
        float t = Mathf.Clamp01(u);
        // High jointAngle is wing-up. Drop streams Flee, not forage flap.
        shoulder = Mathf.Lerp(shoulder, flapMidAngle - 8f * u, t);
        elbow = Mathf.Lerp(elbow, -50f, t);
        wrist = Mathf.Lerp(wrist, 8f, t);
        return true;
    }

    // Faster than forage commute. Same Time.fixedTime as Walk —
    // no extra timer. 1.8 is scramble vs scan, not a new Hz field.
    private float AlarmLookLegWave()
    {
        return (Mathf.PI * 2f) * walkFrequency * 1.8f * Time.fixedTime;
    }

    // BIRD-6 Flee Walk: scramble the dirt gait through the glance.
    // Picture only — DriveLegs still holds ragdoll pose. walkThrust
    // still translates. Hop-turn keeps the about-face. Fly tuck
    // stays Fly. Neck stays 0 so ApplyCryPicture owns the glance.
    // Hen keeps both-leg hop; crow keeps alternating stride; both
    // run the scramble clock. No Find.
    // false = leave the forage gait (alarmLookLegAmp 0).
    private bool ApplyAlarmLookLegPicture()
    {
        float u = AlarmLookLeg01();
        if (u < 0.01f)
            return false;
        float w = AlarmLookLegWave();
        if (walkHopAmp > 0.01f)
        {
            float a = Mathf.Clamp(walkHopAmp, 0f, 1.5f) * u;
            float down = 0.5f + 0.5f * Mathf.Sin(w);
            flockLeftThighZ = flockRightThighZ = -walkHipAmp * a * down;
            flockLeftShankZ = flockRightShankZ = -walkKneeAmp * a * (1f - down);
            if (walkFootAmp > 0.01f)
            {
                float plant = -(flockLeftThighZ + flockLeftShankZ);
                float air = -walkFootAmp * a * (1f - down);
                flockLeftFootZ = flockRightFootZ = plant * down + air;
            }
            else
                flockLeftFootZ = flockRightFootZ = 0f;
            return true;
        }
        float sL = Mathf.Sin(w);
        float sR = Mathf.Sin(w + Mathf.PI);
        flockLeftThighZ = -walkHipAmp * u * sL;
        flockRightThighZ = -walkHipAmp * u * sR;
        float swingL = WalkSwing01(sL);
        float swingR = WalkSwing01(sR);
        flockLeftShankZ = -walkKneeAmp * u * swingL;
        flockRightShankZ = -walkKneeAmp * u * swingR;
        if (walkFootAmp > 0.01f)
        {
            flockLeftFootZ = -(flockLeftThighZ + flockLeftShankZ)
                - walkFootAmp * u * swingL;
            flockRightFootZ = -(flockRightThighZ + flockRightShankZ)
                - walkFootAmp * u * swingR;
        }
        else
            flockLeftFootZ = flockRightFootZ = 0f;
        return true;
    }

    // Ragdoll Flee Walk: same scramble, both hips. false = forage gait.
    private bool ApplyAlarmLookLegs(ref float leftHip, ref float rightHip,
        ref float leftKnee, ref float rightKnee)
    {
        float u = AlarmLookLeg01();
        if (u < 0.01f)
            return false;
        float w = AlarmLookLegWave();
        if (walkHopAmp > 0.01f)
        {
            float a = Mathf.Clamp(walkHopAmp, 0f, 1.5f) * u;
            float down = 0.5f + 0.5f * Mathf.Sin(w);
            leftHip = rightHip = standHipAngle + walkHipAmp * a * down;
            leftKnee = rightKnee = standKneeAngle + walkKneeAmp * a * (1f - down);
            return true;
        }
        float sL = Mathf.Sin(w);
        float sR = Mathf.Sin(w + Mathf.PI);
        leftHip = standHipAngle + walkHipAmp * u * sL;
        rightHip = standHipAngle + walkHipAmp * u * sR;
        leftKnee = standKneeAngle + walkKneeAmp * u * WalkSwing01(sL);
        rightKnee = standKneeAngle + walkKneeAmp * u * WalkSwing01(sR);
        return true;
    }

    // Attack stoop: pin the wings to the body. Picture only —
    // DriveWings still holds ragdoll pose. Lift stays in BirdFlight. No Find.
    // Flock fold −90 is along the back; a dropped shoulder would look like
    // preen. Slight plus presses the box; ulna tucks tighter than Sit −62.
    private void ApplyStoopWingPicture()
    {
        if (stoopWingAmp < 0.01f || mode != BirdMode.Attack)
            return;
        float a = Mathf.Clamp(stoopWingAmp, 0f, 1.5f);
        flockWingFold = -90f + 12f * a;
        flockLeftWingFold = flockRightWingFold = flockWingFold;
        flockUlnaZ = Mathf.Lerp(flockUlnaZ, -74f, Mathf.Clamp01(a));
        flockLeftUlnaZ = flockRightUlnaZ = flockUlnaZ;
    }

    // Ragdoll Attack: drop the shoulder toward the belly and tuck the
    // ulna. false = leave flap-mid / elbow −42 (stoopWingAmp 0).
    private bool ApplyStoopWingPin(ref float shoulder, ref float elbow, ref float wrist)
    {
        if (stoopWingAmp < 0.01f || mode != BirdMode.Attack)
            return false;
        float a = Mathf.Clamp(stoopWingAmp, 0f, 1.5f);
        // High jointAngle is wing-up. Drop streams the dive, not a paused flap.
        shoulder = flapMidAngle - 18f * a;
        elbow = Mathf.Lerp(elbow, -74f, Mathf.Clamp01(a));
        wrist = Mathf.Lerp(wrist, 0f, Mathf.Clamp01(a));
        return true;
    }

    // Attack stoop: hang the feet and point the toes. Picture only —
    // DriveLegs still holds ragdoll pose. Lift stays in BirdFlight. No Find.
    // false = leave the Fly tuck (stoopTalonAmp 0).
    private bool ApplyStoopTalonPicture()
    {
        if (stoopTalonAmp < 0.01f)
            return false;
        float a = Mathf.Clamp(stoopTalonAmp, 0f, 1.5f);
        // Hip 0 hangs under a level body; tuck is −50 against the belly.
        flockLeftThighZ = flockRightThighZ = -8f * a;
        flockLeftShankZ = flockRightShankZ = -14f * a;
        flockLeftFootZ = flockRightFootZ = 22f * a;
        flockNeckZ = -18f * a;
        flockHeadZ = -8f * a;
        return true;
    }

    // Fly/Glide dirt swoop: hang the feet out of the belly-tuck.
    // Picture only — DriveLegs still holds ragdoll pose. Lift stays
    // in BirdFlight. No Find. false = leave the Fly tuck
    // (swoopTalonAmp 0). Flare / Attack / takeoff trail keep their
    // own legs. Air turn still banks after this write.
    private bool ApplySwoopTalonPicture()
    {
        float a = SwoopTalon01();
        if (a < 0.01f)
            return false;
        // Tuck is −50 / −85; flare gear is −10 / −8 plant-ready;
        // Attack toes are −8 / −14 / +22. Mid-window hangs toward
        // gear, not a stoop point and not a plant.
        flockLeftThighZ = flockRightThighZ = Mathf.Lerp(-tuckHipAngle, -14f, Mathf.Clamp01(a));
        flockLeftShankZ = flockRightShankZ = Mathf.Lerp(-tuckKneeAngle, -20f, Mathf.Clamp01(a));
        flockLeftFootZ = flockRightFootZ = 8f * a;
        return true;
    }

    // Sit preen: raise the near (right) wing toward the beak. Fold −90 is
    // along the back; more negative lifts the free end to the head.
    // Picture only — DriveWings still holds ragdoll pose. No Find.
    private void ApplyPreenWingPicture(float amount)
    {
        float p = Mathf.Clamp01(amount);
        if (p < 0.01f)
            return;
        flockRightWingFold = flockWingFold - preenWingLift * p;
        flockRightUlnaZ = flockUlnaZ - 18f * p;
    }

    private void SetPerchEnabled(bool enabled)
    {
        if (bird != null && bird.perchCollider != null)
            bird.perchCollider.enabled = enabled;
    }

    private void DriveWings()
    {
        float shoulder = flapMidAngle;
        float elbow = -12f;
        float wrist = 0f;
        debugDownstroke = false;

        // IsHuntSoil: dirt Walk/Peck after preferDirtLand flicker must
        // not evaluate a flap stroke (lift hops HuntBeak off Rel Hostile).
        if ((mode == BirdMode.Fly || WantsDirtLandingFlap() || BrakingStrokesActive())
            && !IsHuntSoil())
        {
            EvaluateFlap(out shoulder, out elbow, out wrist, out debugDownstroke);
            BlendCruiseGlideWings(ref shoulder, ref elbow, ref debugDownstroke);
            if (ApplyTakeoffWingStroke(ref shoulder, ref elbow))
                wrist = Mathf.Lerp(wrist, 0f, 0.7f);
            ApplyLandingWingFlare(ref shoulder, ref elbow);
            if (ApplyAlarmLookWingPin(ref shoulder, ref elbow, ref wrist))
            {
                // Glance pin: pose spring, not a forage stroke against Flee.
                ControlJointStable(leftShoulderJoint, shoulder, leftShoulderFlexor, leftShoulderExtensor, ref leftShoulderI);
                ControlJointStable(rightShoulderJoint, shoulder, rightShoulderFlexor, rightShoulderExtensor, ref rightShoulderI);
                ControlJointStable(leftElbowJoint, elbow, leftElbowFlexor, leftElbowExtensor, ref leftElbowI);
                ControlJointStable(rightElbowJoint, elbow, rightElbowFlexor, rightElbowExtensor, ref rightElbowI);
                ControlJointStable(leftWristJoint, wrist, leftWristFlexor, leftWristExtensor, ref leftWristI);
                ControlJointStable(rightWristJoint, wrist, rightWristFlexor, rightWristExtensor, ref rightWristI);
                return;
            }
            float hold = CruiseHoldBlend();
            debugGlideBlend = hold;
            // B-TAKEOFF-2: a pocket must not swallow the brake's own strokes —
            // the row under the bird is the one moment the wing may not glide.
            if (!debugBrakeOn && hold > 0.12f)
                wrist = Mathf.Lerp(wrist, 0f, hold);
            if (!debugBrakeOn && hold > 0.55f)
            {
                // Held-wing pocket / dirt swoop: pose spring, not a stroke against a spread wing.
                ControlJointStable(leftShoulderJoint, shoulder, leftShoulderFlexor, leftShoulderExtensor, ref leftShoulderI);
                ControlJointStable(rightShoulderJoint, shoulder, rightShoulderFlexor, rightShoulderExtensor, ref rightShoulderI);
                ControlJointStable(leftElbowJoint, elbow, leftElbowFlexor, leftElbowExtensor, ref leftElbowI);
                ControlJointStable(rightElbowJoint, elbow, rightElbowFlexor, rightElbowExtensor, ref rightElbowI);
                ControlJointStable(leftWristJoint, wrist, leftWristFlexor, leftWristExtensor, ref leftWristI);
                ControlJointStable(rightWristJoint, wrist, rightWristFlexor, rightWristExtensor, ref rightWristI);
                return;
            }
            // Flap gesture. Lift is computed by BirdFlight on the same beat;
            // wing speed no longer feeds thrust.
            DriveShoulderStroke(leftShoulderJoint, shoulder, leftShoulderFlexor, leftShoulderExtensor);
            DriveShoulderStroke(rightShoulderJoint, shoulder, rightShoulderFlexor, rightShoulderExtensor);
            ControlJointStable(leftElbowJoint, elbow, leftElbowFlexor, leftElbowExtensor, ref leftElbowI);
            ControlJointStable(rightElbowJoint, elbow, rightElbowFlexor, rightElbowExtensor, ref rightElbowI);
            ControlJointStable(leftWristJoint, wrist, leftWristFlexor, leftWristExtensor, ref leftWristI);
            ControlJointStable(rightWristJoint, wrist, rightWristFlexor, rightWristExtensor, ref rightWristI);
            return;
        }
        else if (mode == BirdMode.Glide)
        {
            shoulder = glideShoulder;
            elbow = glideElbow;
            wrist = 0f;
            ApplyLandingWingFlare(ref shoulder, ref elbow);
        }
        else if (mode == BirdMode.Attack)
        {
            shoulder = flapMidAngle;
            elbow = -42f;
            wrist = 8f;
            ApplyStoopWingPin(ref shoulder, ref elbow, ref wrist);
        }
        else if (mode == BirdMode.Sit || mode == BirdMode.Peck)
        {
            shoulder = flapMidAngle;
            elbow = -62f;
            wrist = 12f;
            float hunch = SitHunch01();
            if (hunch > 0.01f)
            {
                elbow -= 12f * hunch;
                wrist += 6f * hunch;
            }
            float peckFlick = PeckWingFlick();
            if (peckFlick > 0.01f)
            {
                // High jointAngle is wing-up. Same brace as the flock fold;
                // Sit tuck would keep forage Peck a nodding sit.
                shoulder = Mathf.Min(160f, flapMidAngle + 16f * peckFlick);
                elbow = Mathf.Lerp(elbow, -22f, Mathf.Clamp01(peckFlick));
                wrist = Mathf.Lerp(wrist, 4f, Mathf.Clamp01(peckFlick));
            }
        }
        else
        {
            // Stance and walk: the wing is folded along the back and does not carry.
            shoulder = flapMidAngle;
            elbow = -50f;
            wrist = 10f;
            float flick = WalkHopWingFlick();
            if (flick > 0.01f)
            {
                // High jointAngle is wing-up. Same shrug as the flock fold.
                shoulder = Mathf.Min(160f, flapMidAngle + 18f * flick);
                elbow = Mathf.Lerp(elbow, -16f, Mathf.Clamp01(flick));
                wrist = Mathf.Lerp(wrist, 0f, Mathf.Clamp01(flick));
            }
        }

        float leftShoulder = shoulder;
        float rightShoulder = shoulder;
        float leftElbow = elbow;
        float rightElbow = elbow;
        if (WalkStrideWingActive())
        {
            // High jointAngle is wing-up. Same hitch as the flock fold;
            // opposite the swing thigh so dirt Walk is not a Sit slide.
            float a = Mathf.Clamp(walkStrideWingAmp, 0f, 1.5f);
            float w = (Mathf.PI * 2f) * walkFrequency * Time.fixedTime;
            float sL = Mathf.Sin(w);
            float swingL = WalkSwing01(sL);
            float swingR = WalkSwing01(-sL);
            leftShoulder = Mathf.Min(160f, flapMidAngle + 12f * a * swingR);
            rightShoulder = Mathf.Min(160f, flapMidAngle + 10f * a * swingL);
            leftElbow = Mathf.Lerp(elbow, -18f, Mathf.Clamp01(a * swingR));
            rightElbow = Mathf.Lerp(elbow, -16f, Mathf.Clamp01(a * swingL));
        }
        float walkTurnWing = WalkTurnWing01();
        if (walkTurnWing > 0.01f)
        {
            // High jointAngle is wing-up. Same both-wing shrug
            // as the flock fold; Walk tuck made the hop a twitch.
            leftShoulder = Mathf.Min(160f, flapMidAngle + 18f * walkTurnWing);
            rightShoulder = Mathf.Min(160f, flapMidAngle + 16f * walkTurnWing);
            leftElbow = Mathf.Lerp(elbow, -16f, Mathf.Clamp01(walkTurnWing));
            rightElbow = Mathf.Lerp(elbow, -16f, Mathf.Clamp01(walkTurnWing));
        }
        float alarmWing = AlarmLookWing01();
        if (alarmWing > 0.01f && mode == BirdMode.Walk)
        {
            // High jointAngle is wing-up. Same both-wing shrug
            // as the flock fold; Walk tuck made Flee a commute.
            leftShoulder = Mathf.Min(160f, flapMidAngle + 16f * alarmWing);
            rightShoulder = Mathf.Min(160f, flapMidAngle + 14f * alarmWing);
            leftElbow = Mathf.Lerp(elbow, -22f, Mathf.Clamp01(alarmWing));
            rightElbow = Mathf.Lerp(elbow, -22f, Mathf.Clamp01(alarmWing));
        }
        float cover = 0f;
        if (mode == BirdMode.Sit)
        {
            if (!ApplyLandSettleWings(ref leftShoulder, ref rightShoulder,
                ref leftElbow, ref rightElbow))
            {
                float turnWing = SitTurnWing01();
                if (turnWing > 0.01f)
                {
                    // High jointAngle is wing-up. Same both-wing shrug
                    // as the flock fold; Sit tuck made the hop a twitch.
                    leftShoulder = Mathf.Min(160f, flapMidAngle + 18f * turnWing);
                    rightShoulder = Mathf.Min(160f, flapMidAngle + 16f * turnWing);
                    leftElbow = Mathf.Lerp(elbow, -16f, Mathf.Clamp01(turnWing));
                    rightElbow = Mathf.Lerp(elbow, -16f, Mathf.Clamp01(turnWing));
                }
                float scratchWing = SitScratchWing01();
                if (scratchWing > 0.01f)
                {
                    // High jointAngle is wing-up. Same far-wing shrug
                    // as the flock fold; near stays Sit-tucked.
                    float kick = Mathf.Max(0f, Mathf.Sin(Time.time * 22f));
                    float flick = scratchWing * (0.70f + 0.30f * kick);
                    leftShoulder = Mathf.Min(160f, flapMidAngle + 16f * flick);
                    leftElbow = Mathf.Lerp(elbow, -22f, Mathf.Clamp01(flick));
                }
                float lookWing = SitLookWing01();
                if (Mathf.Abs(lookWing) > 0.01f)
                {
                    // High jointAngle is wing-up. Same far-wing hitch
                    // as the flock fold; near stays Sit-tucked.
                    float hitch = -lookWing;
                    leftShoulder = Mathf.Min(160f, flapMidAngle + 12f * hitch);
                    if (hitch > 0.01f)
                        leftElbow = Mathf.Lerp(elbow, -22f, Mathf.Clamp01(hitch));
                }
                float preenWing = SitPreenWing01();
                if (preenWing > 0.01f)
                {
                    // High jointAngle is wing-up. Same far-wing brace
                    // as the flock fold; near still covers to the beak.
                    leftShoulder = Mathf.Min(160f, flapMidAngle + 12f * preenWing);
                    leftElbow = Mathf.Lerp(elbow, -22f, Mathf.Clamp01(preenWing));
                }
                float sitStartle = AlarmLookSitWing01();
                if (sitStartle > 0.01f)
                {
                    // High jointAngle is wing-up. Same both-wing shrug
                    // as the flock fold; Sit tuck made Flee a tucked perch.
                    leftShoulder = Mathf.Min(160f, flapMidAngle + 16f * sitStartle);
                    rightShoulder = Mathf.Min(160f, flapMidAngle + 14f * sitStartle);
                    leftElbow = Mathf.Lerp(elbow, -22f, Mathf.Clamp01(sitStartle));
                    rightElbow = Mathf.Lerp(elbow, -22f, Mathf.Clamp01(sitStartle));
                }
                float sitHunchStartle = AlarmLookSitHunch01();
                if (sitHunchStartle > 0.01f)
                {
                    // Extra tuck after hitch. Flock look does the
                    // startle hunch; a ragdoll fold would unplant.
                    // Hop-turn already skipped (SitTurnHop zeroes
                    // AlarmLookSit). Pad hunch already ran above.
                    leftElbow -= 12f * sitHunchStartle;
                    rightElbow -= 10f * sitHunchStartle;
                    wrist += 6f * sitHunchStartle;
                }
                if (idlePreen > 0.01f && SitTurnHop() < 0.01f
                    && AlarmLookSit01() < 0.01f)
                    cover = Mathf.Clamp01(idlePreen);
                float roost = NightRoost();
                if (roost > cover)
                    cover = roost * 0.85f;
            }
        }
        float air = AirTurn01();
        if (air > 0.01f && mode == BirdMode.Glide)
        {
            // High jointAngle is wing-up. Fly flap keeps the stroke;
            // held Glide can bank without fighting 7 Hz.
            leftShoulder += 10f * air;
            rightShoulder -= 8f * air;
        }
        if (cover > 0.01f)
        {
            // High jointAngle is wing-up (start of the downstroke). Limits 25…165.
            rightShoulder = flapMidAngle + preenWingLift * 0.75f * cover;
            rightElbow = Mathf.Lerp(elbow, -88f, cover);
        }

        ControlJointStable(leftShoulderJoint, leftShoulder, leftShoulderFlexor, leftShoulderExtensor, ref leftShoulderI);
        ControlJointStable(rightShoulderJoint, rightShoulder, rightShoulderFlexor, rightShoulderExtensor, ref rightShoulderI);
        ControlJointStable(leftElbowJoint, leftElbow, leftElbowFlexor, leftElbowExtensor, ref leftElbowI);
        ControlJointStable(rightElbowJoint, rightElbow, rightElbowFlexor, rightElbowExtensor, ref rightElbowI);
        ControlJointStable(leftWristJoint, wrist, leftWristFlexor, leftWristExtensor, ref leftWristI);
        ControlJointStable(rightWristJoint, wrist, rightWristFlexor, rightWristExtensor, ref rightWristI);
    }

    // Downstroke decreases jointAngle (wing toward the belly); upstroke does the opposite.
    // Do not put D on own speed here: that is viscosity against the flap.
    private void DriveShoulderStroke(HingeJoint2D joint, float targetAngle, Muscle flexor, Muscle extensor)
    {
        float stroke = debugDownstroke ? -1f : 1f;
        float error = 0f;
        if (joint != null)
            error = Mathf.DeltaAngle(joint.jointAngle, targetAngle) / Mathf.Max(1f, jointErrorRef);
        float raw = flapStrokeGain * stroke + flapTrackP * error;
        ApplySignal(Mathf.Clamp(raw, -1f, 1f), flexor, extensor);
    }

    // Asymmetric cycle: downstroke faster than upstroke. On the downstroke the elbow is open
    // (full area); on the upstroke it is slightly folded and the wrist feathers — otherwise
    // the return stroke hits down almost as hard as the power stroke hits up.
    // Frequency follows airspeed: faster flight, faster flap. Phase is accumulated so
    // a Hz change does not jump the stroke.
    private void EvaluateFlap(out float shoulder, out float elbow, out float wrist, out bool downstroke)
    {
        float freq = FlapFrequencyNow();
        if (!flapCycleSeeded)
        {
            flapCycle = Mathf.Repeat(flapPhaseOffset * Mathf.Max(0.5f, flapFrequency), 1f);
            flapCycleSeeded = true;
        }
        flapCycle += Time.fixedDeltaTime * freq;
        // B-TAKEOFF-2: a wrap is one stroke spent; the burst ends when its own
        // count runs out and the cruise rate takes over (EvaluateFlap reads the
        // countdown through FlapFrequencyNow).
        if (flapCycle >= 1f)
        {
            flapCycle -= Mathf.Floor(flapCycle);
            if (takeoffStrokesLeft > 0)
                takeoffStrokesLeft--;
            else if (debugBrakeOn && brakeStrokesLeft > 0)
            {
                // B-TAKEOFF-2: one deep row spent. At zero the wing stops rowing
                // and holds for the flare.
                brakeStrokesLeft--;
                debugBrakesFired++;
            }
        }
        flapCycle = Mathf.Repeat(flapCycle, 1f);
        float t = flapCycle;
        debugFlapPhase = t;

        float down = Mathf.Clamp(downstrokeFraction, 0.25f, 0.5f);
        float s;
        if (t < down)
        {
            downstroke = true;
            s = Mathf.Lerp(1f, -1f, t / down);
        }
        else
        {
            downstroke = false;
            s = Mathf.Lerp(-1f, 1f, (t - down) / Mathf.Max(0.05f, 1f - down));
        }

        // B-TAKEOFF-2: the brake's row is deeper than the cruise stroke.
        float amp = flapAmplitude;
        if (debugBrakeOn && brakeStrokeAmp > 1f)
            amp *= brakeStrokeAmp;
        // B-WING-3: the burst's own sweep — the first strokes off the ground
        // are the wide power stroke the eyes asked for.
        if (takeoffStrokesLeft > 0 && deployBurstAmp > 1f)
            amp *= deployBurstAmp;
        shoulder = flapMidAngle + amp * s;
        elbow = downstroke ? -5f : -upstrokeElbowFlex;
        wrist = downstroke ? 0f : upstrokeWristFeather;
    }

    // Fly pocket or dirt swoop: spread the wing instead of beating.
    // Blend is from BirdFlight (seeded, staggered). Does not write mode.
    private void BlendCruiseGlideWings(ref float shoulder, ref float elbow, ref bool downstroke)
    {
        if (mode != BirdMode.Fly || flight == null)
            return;
        float g = CruiseHoldBlend();
        if (g < 0.12f)
            return;
        shoulder = Mathf.Lerp(shoulder, glideShoulder, g);
        elbow = Mathf.Lerp(elbow, glideElbow, g);
        if (g > 0.55f)
            downstroke = false;
    }

    // Max of held-wing pocket and dirt-swoop envelope. Cached flight, no Find.
    private float CruiseHoldBlend()
    {
        if (flight == null)
            return 0f;
        return Mathf.Max(flight.cruiseGlideBlend, flight.swoopLook);
    }

    // Crow: Hz follows airspeed. Chicken / woodpecker hops stay at flapFrequency.
    // Climb keeps a full beat so takeoff is not a lazy stroke.
    private float FlapFrequencyNow()
    {
        float baseHz = Mathf.Max(0.5f, flapFrequency);
        // B-TAKEOFF-2: the first strokes off the ground are the takeoff burst —
        // 3…6 of them at flapTakeoffHz, whatever the airspeed says. The bird
        // then falls back to its own cruise rate, so the ratio between the two
        // is the round's reading (bar: >= 1.4x). Only the airspeed-scaling
        // flier (the crow) has this cadence: the hen's hop and the pecker's
        // flutter keep their own rates.
        if (takeoffStrokesLeft > 0 && flapTakeoffHz > 0.05f
            && (bird == null || bird.ScalesFlapWithAirspeed))
        {
            debugFlapHz = Mathf.Max(baseHz, flapTakeoffHz);
            return debugFlapHz;
        }
        if (bird != null && !bird.ScalesFlapWithAirspeed)
        {
            debugFlapHz = baseHz;
            return baseHz;
        }
        if (BrakingStrokesActive())
        {
            // The row under the bird: slow and deep beats, one per stroke.
            debugFlapHz = Mathf.Max(0.5f, brakeStrokeHz);
            return debugFlapHz;
        }
        if (WantsDirtLandingFlap())
        {
            debugFlapHz = flapLandingHz > 0.05f
                ? flapLandingHz
                : baseHz * Mathf.Clamp(landFlapScale, 0.2f, 0.85f);
            return debugFlapHz;
        }
        if (bodyRb == null || mode != BirdMode.Fly)
        {
            debugFlapHz = baseHz;
            return baseHz;
        }

        if (Bird.FlapCouple && flight != null)
        {
            // B-FLAP-1: the cruise rate follows the lift demand (measured
            // ≈1.16 at cruise, 1.5-2.3 in a climb): 1.2 × root puts cruise at
            // ~4.26 Hz and the 1.68 top caps a climb at 5.54 — inside the
            // request's 3.9-4.4 / 4.8-5.6 bars.
            float demand = Mathf.Clamp(flight.lastHoverDemand, 0.5f, 2.5f);
            debugFlapHz = baseHz * Mathf.Clamp(1.2f * Mathf.Sqrt(demand), flapFreqMinScale, 1.68f);
            return debugFlapHz;
        }

        float cruise = flapSpeedRef;
        if (cruise < 0.05f)
            cruise = flight != null && flight.cruiseSpeed > 0.05f ? flight.cruiseSpeed : 2.2f;

        float speed = bodyRb.linearVelocity.magnitude;
        float u = Mathf.Clamp01(speed / Mathf.Max(0.4f, cruise));
        float scale = Mathf.Lerp(flapFreqMinScale, flapFreqMaxScale, u);
        if (bodyRb.linearVelocity.y > 0.3f)
            scale = Mathf.Max(scale, 1f);
        debugFlapHz = baseHz * scale;
        return debugFlapHz;
    }

    // B-TAKEOFF-2: arm the burst at the takeoff edge. The count of strokes is
    // the 3…6 band, jittered by the member's own phase offset so fifty birds do
    // not beat in lockstep. Called from StartTakeoffAirClocks (the same edge
    // the wing cock and the trail use).
    private void ArmTakeoffBurst()
    {
        if (flapTakeoffStrokes <= 0 || flapTakeoffHz <= 0.05f)
            return;
        if (bird != null && !bird.ScalesFlapWithAirspeed)
            return;
        if (takeoffBurstStrokes <= 0)
        {
            float s = Mathf.Repeat(flapPhaseOffset * 7.13f, 1f);
            int jitter = Mathf.Clamp((int)(s * 4f), 0, 3);
            takeoffBurstStrokes = Mathf.Clamp(flapTakeoffStrokes + jitter - 1, 3, 6);
        }
        takeoffStrokesLeft = takeoffBurstStrokes;
        takeoffBursts++;
    }

    private void DriveLegs(bool flying)
    {
        float leftHip = standHipAngle;
        float rightHip = standHipAngle;
        float leftKnee = standKneeAngle;
        float rightKnee = standKneeAngle;
        bool onGround = sensors.leftFootGrounded || sensors.rightFootGrounded;
        // Leftover Fly/Glide/Attack while IsHuntSoil is a dirt hunt.
        // Air-tuck / landing-gear on that leftover mode folded the
        // ragdoll off the Insect, so BeakStrike never Rel Hostile.
        // IsHuntSoil covers dirt Walk/Peck after preferDirtLand flicker.
        bool airPose = flying && !IsHuntSoil() && !onGround;

        if (airPose)
        {
            // Flare pitches the chest; do not wait for |pitch| < 25.
            if (WantsLandingGear())
            {
                float a = Mathf.Clamp(landGearAmp, 0f, 1.5f);
                leftHip = rightHip = 12f * a;
                leftKnee = rightKnee = 18f * a;
                ControlJointStable(leftHipJoint, leftHip, leftHipFlexor, leftHipExtensor, ref leftHipI);
                ControlJointStable(rightHipJoint, rightHip, rightHipFlexor, rightHipExtensor, ref rightHipI);
                ControlJointStable(leftKneeJoint, leftKnee, leftKneeFlexor, leftKneeExtensor, ref leftKneeI);
                ControlJointStable(rightKneeJoint, rightKnee, rightKneeFlexor, rightKneeExtensor, ref rightKneeI);
                ControlJointStable(leftAnkleJoint, 0f, leftAnkleFlexor, leftAnkleExtensor, ref leftAnkleI);
                ControlJointStable(rightAnkleJoint, 0f, rightAnkleFlexor, rightAnkleExtensor, ref rightAnkleI);
                return;
            }
            float trail = TakeoffTrail01();
            if (trail > 0.01f)
            {
                float hang = Mathf.Clamp01(trail);
                float u = 1f - hang;
                u *= u;
                float a = Mathf.Clamp(takeoffTrailAmp, 0f, 1.5f);
                leftHip = rightHip = Mathf.Lerp(6f * a, tuckHipAngle, u);
                leftKnee = rightKnee = Mathf.Lerp(10f * a, tuckKneeAngle, u);
                ControlJointStable(leftHipJoint, leftHip, leftHipFlexor, leftHipExtensor, ref leftHipI);
                ControlJointStable(rightHipJoint, rightHip, rightHipFlexor, rightHipExtensor, ref rightHipI);
                ControlJointStable(leftKneeJoint, leftKnee, leftKneeFlexor, leftKneeExtensor, ref leftKneeI);
                ControlJointStable(rightKneeJoint, rightKnee, rightKneeFlexor, rightKneeExtensor, ref rightKneeI);
                ControlJointStable(leftAnkleJoint, 12f * a * hang, leftAnkleFlexor, leftAnkleExtensor, ref leftAnkleI);
                ControlJointStable(rightAnkleJoint, 12f * a * hang, rightAnkleFlexor, rightAnkleExtensor, ref rightAnkleI);
                return;
            }
        }

        if (airPose && Mathf.Abs(sensors.bodyPitch) < 25f)
        {
            bool landingGlide = mode == BirdMode.Glide && bodyRb != null && flight != null
                && (bodyRb.position.y - Bird.StanceRootY(bird)) < Mathf.Max(0.4f, flight.flareHeight);
            if (!landingGlide)
            {
                // Attack hangs the feet (talons), not the Fly belly-tuck.
                if (mode == BirdMode.Attack && stoopTalonAmp > 0.01f)
                {
                    float a = Mathf.Clamp(stoopTalonAmp, 0f, 1.5f);
                    leftHip = rightHip = 8f * a;
                    leftKnee = rightKnee = 18f * a;
                    ControlJointStable(leftHipJoint, leftHip, leftHipFlexor, leftHipExtensor, ref leftHipI);
                    ControlJointStable(rightHipJoint, rightHip, rightHipFlexor, rightHipExtensor, ref rightHipI);
                    ControlJointStable(leftKneeJoint, leftKnee, leftKneeFlexor, leftKneeExtensor, ref leftKneeI);
                    ControlJointStable(rightKneeJoint, rightKnee, rightKneeFlexor, rightKneeExtensor, ref rightKneeI);
                    ControlJointStable(leftAnkleJoint, 22f * a, leftAnkleFlexor, leftAnkleExtensor, ref leftAnkleI);
                    ControlJointStable(rightAnkleJoint, 22f * a, rightAnkleFlexor, rightAnkleExtensor, ref rightAnkleI);
                    return;
                }
                // Dirt swoop hangs toward flare gear, not Attack toes
                // and not the plant. Same pose springs as landGear.
                float swoopTalon = SwoopTalon01();
                if (swoopTalon > 0.01f)
                {
                    float u = Mathf.Clamp01(swoopTalon);
                    leftHip = rightHip = Mathf.Lerp(tuckHipAngle, 14f, u);
                    leftKnee = rightKnee = Mathf.Lerp(tuckKneeAngle, 20f, u);
                    float ankle = 10f * u;
                    ControlJointStable(leftHipJoint, leftHip, leftHipFlexor, leftHipExtensor, ref leftHipI);
                    ControlJointStable(rightHipJoint, rightHip, rightHipFlexor, rightHipExtensor, ref rightHipI);
                    ControlJointStable(leftKneeJoint, leftKnee, leftKneeFlexor, leftKneeExtensor, ref leftKneeI);
                    ControlJointStable(rightKneeJoint, rightKnee, rightKneeFlexor, rightKneeExtensor, ref rightKneeI);
                    ControlJointStable(leftAnkleJoint, ankle, leftAnkleFlexor, leftAnkleExtensor, ref leftAnkleI);
                    ControlJointStable(rightAnkleJoint, ankle, rightAnkleFlexor, rightAnkleExtensor, ref rightAnkleI);
                    return;
                }
                leftHip = rightHip = tuckHipAngle;
                leftKnee = rightKnee = tuckKneeAngle;
                ControlJointStable(leftHipJoint, leftHip, leftHipFlexor, leftHipExtensor, ref leftHipI);
                ControlJointStable(rightHipJoint, rightHip, rightHipFlexor, rightHipExtensor, ref rightHipI);
                ControlJointStable(leftKneeJoint, leftKnee, leftKneeFlexor, leftKneeExtensor, ref leftKneeI);
                ControlJointStable(rightKneeJoint, rightKnee, rightKneeFlexor, rightKneeExtensor, ref rightKneeI);
                ControlJointStable(leftAnkleJoint, 0f, leftAnkleFlexor, leftAnkleExtensor, ref leftAnkleI);
                ControlJointStable(rightAnkleJoint, 0f, rightAnkleFlexor, rightAnkleExtensor, ref rightAnkleI);
                return;
            }
        }

        if ((mode == BirdMode.Sit || mode == BirdMode.Peck) && onGround)
        {
            leftHip = rightHip = sitHipAngle;
            leftKnee = rightKnee = standKneeAngle + 12f;
            float crouch = SitCrouch01();
            if (crouch > 0.01f)
            {
                // Modest extra tuck. Flock look does the wrap;
                // a ragdoll squat would unplant.
                leftHip += 8f * crouch;
                rightHip += 6f * crouch;
                leftKnee += 10f * crouch;
                rightKnee += 8f * crouch;
            }
            float lunge = PeckLunge01();
            if (lunge > 0.01f)
            {
                // Modest extra tuck. Flock look does the jab crouch;
                // a ragdoll squat would unplant.
                leftHip += 10f * lunge;
                rightHip += 8f * lunge;
                leftKnee += 12f * lunge;
                rightKnee += 10f * lunge;
            }
            if (mode == BirdMode.Sit)
            {
                float hop = SitTurnHop();
                if (hop > 0.01f)
                {
                    // Modest extra tuck. Flock look does the hop-turn;
                    // a ragdoll swing would unplant.
                    leftHip = sitHipAngle + 10f * hop;
                    rightHip = sitHipAngle + 8f * hop;
                    leftKnee = standKneeAngle + 12f + 12f * hop;
                    rightKnee = standKneeAngle + 12f + 10f * hop;
                }
                else if (idleHop > 0.01f && NightRoost() < 0.35f
                    && LandSettle01() < 0.01f && AlarmLookSit01() < 0.01f)
                {
                    if (sitScratchAmp > 0.01f)
                    {
                        // Modest extra tuck on the near hip. Flock look does
                        // the full raise; a ragdoll swing would unplant.
                        float a = Mathf.Clamp(sitScratchAmp, 0f, 1.5f) * idleHop;
                        rightHip = sitHipAngle + 16f * a;
                        rightKnee = standKneeAngle + 12f + 20f * a;
                    }
                    else
                    {
                        leftHip -= 10f * idleHop;
                        rightHip -= 6f * idleHop;
                    }
                }
                float startleLeg = AlarmLookSitLeg01();
                if (startleLeg > 0.01f)
                {
                    // Modest extra tuck. Flock look does the startle
                    // crouch; a ragdoll squat would unplant. Hop-turn
                    // already skipped (SitTurnHop zeroes AlarmLookSit).
                    // Startle grasp is flock look (ankles stay CoM).
                    leftHip += 10f * startleLeg;
                    rightHip += 8f * startleLeg;
                    leftKnee += 12f * startleLeg;
                    rightKnee += 10f * startleLeg;
                }
            }
        }

        if (mode == BirdMode.Fly && onGround && takeoffCrouchAmp > 0.01f)
        {
            float a = Mathf.Clamp(takeoffCrouchAmp, 0f, 1.5f);
            leftHip = rightHip = sitHipAngle * a;
            leftKnee = rightKnee = standKneeAngle + 16f * a;
        }

        if (mode == BirdMode.Walk)
        {
            float hop = WalkTurnHop();
            if (hop > 0.01f)
            {
                // Modest extra tuck. Flock look does the plant-hop;
                // a ragdoll swing would unplant.
                leftHip = rightHip = standHipAngle + 12f * hop;
                leftKnee = rightKnee = standKneeAngle + 14f * hop;
            }
            else if (ApplyAlarmLookLegs(ref leftHip, ref rightHip, ref leftKnee, ref rightKnee))
            {
                // Brain already wrote Walk (hen Flee). Flock look does
                // the scramble; a ragdoll swing would unplant.
            }
            else if (!ApplyWalkHopLegs(ref leftHip, ref rightHip, ref leftKnee, ref rightKnee)
                && !ApplySearchCrouchLegs(ref leftHip, ref rightHip, ref leftKnee, ref rightKnee))
            {
                float w = (Mathf.PI * 2f) * walkFrequency * Time.fixedTime;
                float sL = Mathf.Sin(w);
                float sR = Mathf.Sin(w + Mathf.PI);
                leftHip = standHipAngle + walkHipAmp * sL;
                rightHip = standHipAngle + walkHipAmp * sR;
                leftKnee = standKneeAngle + walkKneeAmp * WalkSwing01(sL);
                rightKnee = standKneeAngle + walkKneeAmp * WalkSwing01(sR);
            }
        }

        // Load-bearing but light: stable PD. Limb inertia is raised by Bird.minLimbInertia;
        // otherwise the denominator chokes the torque (bird_stand) or explicit PD diverges (bird_stand3).
        ControlJointStable(leftHipJoint, leftHip, leftHipFlexor, leftHipExtensor, ref leftHipI);
        ControlJointStable(rightHipJoint, rightHip, rightHipFlexor, rightHipExtensor, ref rightHipI);
        ControlJointStable(leftKneeJoint, leftKnee, leftKneeFlexor, leftKneeExtensor, ref leftKneeI);
        ControlJointStable(rightKneeJoint, rightKnee, rightKneeFlexor, rightKneeExtensor, ref rightKneeI);
        // Walk translation is body thrust, not ankle push on the dirt.
        // Leftover Fly on a hunt plate must not run ankle CoM PD: that
        // torque turns the whole body about the toe and hops the HuntBeak
        // slab off the Insect (BeakStrike never Rel Hostile). IsHuntSoil
        // covers dirt Walk/Peck after preferDirtLand flicker.
        if (mode != BirdMode.Walk && !IsHuntSoil())
            DriveAnklesForBalance();
    }

    // Walk is a status (BirdMode.Walk). Horizontal force on the body,
    // speed cap walkSpeed. Legs are look / pose, not a ground paddle.
    // ---- B-GROUND-1 "The claws hold" (the owner's sheet "Трение когтей", 25.09) ----
    // The pads themselves are Bird.BuildClaw: real colliders, explicit
    // PhysicsMaterial2D. What this block owns is WHEN a pad is down, that the
    // claw is not dragged on its own plant, and the gait's clock — driven by the
    // bird's own travel while the grip is on, so the sweep of the legs and the
    // body's advance agree and the planted claw has nothing to skate against.
    // Grip off (no -birdClawGrip): every expression below is skipped and the
    // Walk keeps the shape the earlier rounds were accepted with.

    private bool ClawGripActive()
    {
        return (Bird.ClawGrip || Bird.NoSlide) && flockRig && bird != null
            && (bird.leftClaw != null || bird.rightClaw != null);
    }

    // The pads touch the dirt only when they have a load to carry
    // (-birdClawShare). With no share the hover holds the bird and a pad on the
    // ground would be a contact with no normal force at all: the claws there are
    // the picture's plant, and the gait below is what keeps it from skating.
    private bool ClawPadsSolid()
    {
        return ClawGripActive() && bird.ClawWeightShare() > 0.001f;
    }

    // How much of the bird's weight a planted claw carries right now.
    private float ClawCarryShare()
    {
        if (!ClawPadsSolid())
            return 0f;
        return (clawPlanted[0] || clawPlanted[1]) ? bird.ClawWeightShare() : 0f;
    }

    // The gait's own clock. Grip off: the old expression, bit for bit. Grip on:
    // cycles of the bird's travel, so the stride and the advance cannot drift.
    private float WalkGaitPhase()
    {
        if (!ClawGripActive())
            return walkFrequency * Time.fixedTime;
        return clawGaitCycles;
    }

    private float ClawStrideMeters()
    {
        if (Bird.ClawStrideKnob >= 0f)
            return Mathf.Max(0.04f, Bird.ClawStrideKnob);
        if (clawStrideMeters > 0.001f)
            return clawStrideMeters;
        if (Bird.StepAmpKnob > 0f || Bird.NoSlide)
            // B-VIS-WALK: the drawn step's own geometry - the sweep back over
            // the plant is 2*reach and the plant is half the cycle, so the
            // clock's cycle is 4*reach. The observed sweep would double it (the
            // swing half reads the world frame), so the step names its own.
            return Mathf.Clamp(4f * StepReachMeters(), 0.04f, 0.6f);
        if (clawStrideObs > 0.005f)
            // The stance runs half the cycle and must swallow the picture's own
            // sweep exactly: the step = the sweep / 0.5. A gait that matches the
            // legs leaves the pin nothing to correct, which is what the slip
            // column reads.
            return Mathf.Clamp(clawStrideObs * 2f, 0.06f, 0.6f);
        return 0.18f;
    }

    // ---- B-VIS-WALK "the visible walk" (-birdStepAmp) ----
    // Picture only: the ankle rides a stroke in the leg's own plane - a sweep
    // from +reach (the plant) to -reach (the toe-off) through the claw's own
    // down band, then a folded, lifted return - and the thigh and shank are
    // solved to it by a two-link IK. The pads ride the foot, so the claw
    // columns read this very stroke; no force and no collider read it. The
    // clock is WalkGaitPhase (the travel one under -birdClawGrip), so the
    // drawn plant and the travel cannot drift.
    private bool StepPictureActive()
    {
        return (Bird.StepAmpKnob > 0f || Bird.NoSlide) && bird != null && flockRig
            && mode == BirdMode.Walk
            && ((sensors != null && sensors.bothGrounded) || OnHuntDirt());
    }

    private float StepReachMeters()
    {
        float a = bird != null ? bird.thighSize.y : 0.04f;
        float b = bird != null ? bird.shankSize.y : 0.055f;
        float reach = Bird.StepReachKnob >= 0f ? Bird.StepReachKnob : 0.028f;
        if (Bird.StepHzKnob > 0f)
            // B-WALK-1 (default off): the crow's 0.12 m step needs 0.62 x the leg.
            return Mathf.Clamp(reach, 0.004f, (a + b) * 0.62f);
        return Mathf.Clamp(reach, 0.004f, (a + b) * 0.45f);
    }

    private float StepLiftMeters()
    {
        return Bird.StepLiftKnob >= 0f ? Mathf.Max(0f, Bird.StepLiftKnob) : 0.02f;
    }

    private void ApplyStepPicture()
    {
        float phase = WalkGaitPhase();
        StepLegAngles(Mathf.Repeat(phase - 0.25f, 1f), out float lt, out float ls, out float lf);
        StepLegAngles(Mathf.Repeat(phase - 0.75f, 1f), out float rt, out float rs, out float rf);
        SetThigh(bird.leftThigh, lt);
        SetThigh(bird.rightThigh, rt);
        SetShank(bird.leftShank, ls, ref lastLeftShankZ);
        SetShank(bird.rightShank, rs, ref lastRightShankZ);
        SetShank(bird.leftFoot, lf, ref lastLeftFootZ);
        SetShank(bird.rightFoot, rf, ref lastRightFootZ);
        debugStepOn = 1;
        debugStepPhase = phase;
    }

    // One leg's step, leg-local: +x forward (the toes), -y down from the hip.
    // q is that foot's own cycle: [0..0.5) the plant (a sweep front to back on
    // the standing arc - the paw holds its dirt), [0.5..1) the swing (the leg
    // folds and lifts, then lands in front again).
    private void StepLegAngles(float q, out float thighZ, out float shankZ, out float footZ)
    {
        float a = bird.thighSize.y;
        float b = bird.shankSize.y;
        float reach = StepReachMeters();
        float lift = StepLiftMeters();
        float x, y;
        if (q < 0.5f)
        {
            float u = q * 2f;
            x = reach * (1f - 2f * u);
            y = -StepStandHeight(x, a, b);
        }
        else
        {
            float v = q * 2f - 1f;
            x = reach * (2f * v - 1f);
            y = -StepStandHeight(x, a, b) + lift * Mathf.Sin(v * Mathf.PI);
        }
        float d = Mathf.Sqrt(x * x + y * y);
        float maxD = a + b - 0.0006f;
        if (d > maxD && d > 1e-6f)
        {
            x *= maxD / d;
            y *= maxD / d;
            d = maxD;
        }
        // Knee-back: the thigh swings back, the shank reaches down-forward to
        // the ankle - the bird's own Z, and the sign family the flock hip used.
        float baseAng = Mathf.Atan2(x, -y);
        float delta = Mathf.Acos(Mathf.Clamp((a * a + d * d - b * b) / (2f * a * d), -1f, 1f));
        float thighAng = baseAng - delta;
        float kneeX = a * Mathf.Sin(thighAng);
        float kneeY = -a * Mathf.Cos(thighAng);
        float shankAng = Mathf.Atan2(x - kneeX, -(y - kneeY));
        thighZ = thighAng * Mathf.Rad2Deg;
        shankZ = (shankAng - thighAng) * Mathf.Rad2Deg;
        footZ = -(thighZ + shankZ);
    }

    private static float StepStandHeight(float x, float a, float b)
    {
        // The ankle rides the arc of a nearly straight leg: the paw stays at
        // the dirt through the plant (its ends rise a few millimetres).
        float r = a + b - 0.0006f;
        float h2 = r * r - x * x;
        return h2 > 0.0001f ? Mathf.Sqrt(h2) : 0.01f;
    }

    private float ClawWalkCap()
    {
        if (Bird.StepHzKnob > 0f)
            // B-WALK-1 (default off): the cap follows the step and the cadence,
            // (stride / 2) x steps/s; -birdClawSpeed is ignored under the switch.
            return 0.5f * ClawStrideMeters() * Bird.StepHzKnob;
        return Bird.ClawSpeedKnob >= 0f
            ? Mathf.Max(0f, Bird.ClawSpeedKnob)
            : clawWalkSpeed;
    }

    // ---- B-GROUND-2 "stand or walk" (-birdNoSlide) ----
    // The owner's rule for the dirt: a bird is either standing or walking. A
    // grounded flock body's horizontal speed is held to the gait's own pace
    // while Walk drives it, and to a stand in every other mode. The brake is
    // the walk drive's own authority (walkThrust), so the legs are never asked
    // to carry a body that outruns them. Airborne is untouched — a leap, a hop
    // or a flight leaves the governor behind the moment no foot is down.
    private void ApplyGroundGovernor()
    {
        if (!Bird.NoSlide || !flockRig || sensors == null || bodyRb == null)
            return;
        if (!OnDirtSupport() && !OnHuntDirt())
            return;
        float vx = bodyRb.linearVelocity.x;
        float target = mode == BirdMode.Walk ? ClawWalkCap() : 0f;
        if (Mathf.Abs(vx) - target <= groundGovernorBand)
            return;
        float brake = Bird.NoSlideBrakeKnob >= 0f
            ? Mathf.Max(0f, Bird.NoSlideBrakeKnob)
            : groundGovernorBrake * walkThrust;
        bodyRb.AddForce(new Vector2(-Mathf.Sign(vx) * brake, 0f), ForceMode2D.Force);
    }

    // The pad's own point in the world, straight off the picture's bones (no
    // physics sync lag).
    private Vector3 ClawPadPoint(int side)
    {
        BoxCollider2D pad = side == 0 ? bird.leftClaw : bird.rightClaw;
        Transform foot = side == 0 ? bird.leftFoot : bird.rightFoot;
        if (pad == null || foot == null)
            return Vector3.zero;
        return foot.TransformPoint(new Vector3(pad.offset.x, pad.offset.y, 0f));
    }

    // Called at the end of LateUpdate, after the flock pose is on the bones.
    private void ClawGripLate()
    {
        if (!ClawGripActive() || bodyRb == null)
            return;
        bool onDirt = (sensors != null && sensors.bothGrounded) || OnHuntDirt();
        // Walk and Peck only: a bird that is walking or foraging. Sitting, the
        // legs tuck and the pads would be pressed into the dirt.
        bool groundMode = mode == BirdMode.Walk || mode == BirdMode.Peck;
        if (!onDirt || !groundMode)
        {
            // Airborne or on a pad: no claw holds, and the clock goes back to
            // the physics one so a Walk that starts elsewhere keeps its phase.
            ClawRelease(0);
            ClawRelease(1);
            clawGaitCycles = walkFrequency * Time.fixedTime;
            clawMinRelY[0] = clawMinRelY[1] = float.MaxValue;
            clawSwingMin[0] = clawSwingMin[1] = float.MaxValue;
            clawSwingMax[0] = clawSwingMax[1] = float.MinValue;
            return;
        }
        Vector2 body = bodyRb.position;
        float dz = body.x - clawLastX;
        clawLastX = body.x;
        clawGaitCycles = Mathf.Repeat(clawGaitCycles + Mathf.Abs(dz) / ClawStrideMeters(), 1f);
        ClawPadStep(0, body);
        ClawPadStep(1, body);
        debugClawStride = ClawStrideMeters();
    }

    private void ClawPadStep(int side, Vector2 body)
    {
        BoxCollider2D col = side == 0 ? bird.leftClaw : bird.rightClaw;
        Transform foot = side == 0 ? bird.leftFoot : bird.rightFoot;
        if (col == null || foot == null)
            return;
        Vector3 pad = foot.TransformPoint(new Vector3(col.offset.x, col.offset.y, 0f));
        float relX = pad.x - body.x;
        float relY = pad.y - body.y;
        // B-GROUND-1: the plant is a band of the gait's own phase, not a height
        // test. Read off the picture (world x of the pad over the body, the bird
        // walking -x): the pad reaches its lowest and most forward point at phase
        // 0.25 for the near foot and 0.75 for the other, and its stance is the
        // half where it sweeps back over the body (0.25..0.75). The two feet tile
        // the cycle, so a claw holds the ground at every phase. A height test on
        // a foot whose whole lift is a few mm kept the window open for the whole
        // walk — a claw dragged along the dirt.
        float ph = clawGaitCycles;
        bool down = side == 0
            ? (ph >= 0.25f && ph < 0.75f)
            : (ph >= 0.75f || ph < 0.25f);
        if (!down)
        {
            // The swing half: the picture's own reach, for the gait's step.
            if (relX < clawSwingMin[side]) clawSwingMin[side] = relX;
            if (relX > clawSwingMax[side]) clawSwingMax[side] = relX;
        }
        else
        {
            float sweep = clawSwingMax[side] - clawSwingMin[side];
            if (sweep > 0.01f && sweep < 0.3f)
                clawStrideObs = clawStrideObs <= 0f
                    ? sweep
                    : Mathf.Lerp(clawStrideObs, sweep, 0.25f);
            clawSwingMin[side] = float.MaxValue;
            clawSwingMax[side] = float.MinValue;
        }
        if (down)
        {
            if (!clawPlanted[side])
            {
                clawPlanted[side] = true;
                clawPlantX[side] = pad.x;
                clawPlantRelX[side] = relX;
                clawPinUsed[side] = 0f;
                clawSlipMax[side] = 0f;
                clawSlipNow[side] = 0f;
                if (foot != null)
                    clawFootHome[side] = foot.localPosition;
                if (col != null)
                    col.enabled = ClawPadsSolid();
            }
            float slip = Mathf.Abs(pad.x - clawPlantX[side]);
            clawSlipNow[side] = slip;
            if (slip > clawSlipMax[side])
                clawSlipMax[side] = slip;
            // The pin: the picture's foot is nudged so the pad stays on its own
            // plant. Small — the distance-driven clock carries the gait; this
            // only takes out the residue (a plate edge, an outside push).
            if (Bird.ClawPin && foot != null && foot.parent != null
                && clawPinUsed[side] < clawPinBudget)
            {
                float want = clawPlantX[side] - pad.x;
                float step = Mathf.Clamp(want, -clawPinStep, clawPinStep);
                if (Mathf.Abs(step) > 1e-5f)
                {
                    clawPinUsed[side] += Mathf.Abs(step);
                    foot.localPosition += foot.parent.InverseTransformVector(new Vector3(step, 0f, 0f));
                }
            }
        }
        else if (clawPlanted[side])
        {
            ClawRelease(side);
        }
        if (side == 0)
        {
            debugClawLX = pad.x;
            debugClawLY = pad.y;
            debugClawLOn = clawPlanted[0] ? 1 : 0;
            debugClawSlipL = clawSlipNow[0];
        }
        else
        {
            debugClawRX = pad.x;
            debugClawRY = pad.y;
            debugClawROn = clawPlanted[1] ? 1 : 0;
            debugClawSlipR = clawSlipNow[1];
        }
    }

    // The claw lets go: the pad goes back to its build state (disabled) and the
    // foot's picture back to its own place, so a released foot is the old foot.
    private void ClawRelease(int side)
    {
        if (!clawPlanted[side])
            return;
        clawPlanted[side] = false;
        BoxCollider2D col = side == 0 ? bird.leftClaw : bird.rightClaw;
        Transform foot = side == 0 ? bird.leftFoot : bird.rightFoot;
        if (col != null)
            col.enabled = false;
        if (foot != null)
            foot.localPosition = clawFootHome[side];
    }

    private void ApplyWalkThrust()
    {
        if (sensors == null || bodyRb == null)
            return;
        // Hunt Walk is written as soon as the brain sees the Insect
        // (noticeRange), including leftover Fly still above DirtHuntHeight.
        // Gating thrust on OnDirtPlate left vx=0 at 4 m: gravity takes
        // the body down on the spawn X and BeakStrike never Rel Hostile.
        // Leftover Fly/Glide/Attack used to skip this until OnHuntDirt
        // (height < 0.45 m): a hover at perchCruiseHeight never closed
        // along X. Close with walkThrust in the air too; eat stays the beak.
        // Sleep here is leftover Sit: AddForce on a sleeping flock body
        // used to drop the first hunt step, so Rel Hostile never closed.
        // IsHuntSoil covers preferDirtLand flicker on dirt Walk/Peck.
        if (IsHuntSoil() && bodyRb.IsSleeping())
            bodyRb.WakeUp();
        // Peck is the eat window. Brain writes Peck in InPeckWindow
        // (Pursue close), not OnDirtPlate — leftover hover / NearPerch
        // used to skip the first Peck and keep HuntBeak Walk-parked.
        // Halt vx on the whole Peck so leftover Fly does not walk the
        // body-X strip off the Insect before Rel Hostile. Hurt is BeakStrike.
        // Keep the halt if preferDirtLand drops one Update before Hurt.
        if (mode == BirdMode.Peck && IsHuntSoil())
        {
            Vector2 halt = bodyRb.linearVelocity;
            halt.x = 0f;
            float peckH = bodyRb.position.y - Bird.StanceRootY(bird);
            if (peckH < 0.04f)
                halt.y = 0f;
            bodyRb.linearVelocity = halt;
            return;
        }
        if (mode == BirdMode.Walk)
        {
            // Dirt Walk after flicker: IsHuntSoil (near dirt) must still
            // close — preferDirtLand alone parked walkThrust until
            // bothGrounded and BeakStrike never Rel Hostile.
            if (!IsHuntSoil() && !sensors.bothGrounded)
                return;
        }
        else if (!(IsHuntSoil() && IsFlightMode()))
            return;
        // Forage about-face plants; insect hunt (IsHuntSoil) must
        // keep closing. Brain writes Walk — eat is still the beak.
        if (WalkTurnHop() > 0.01f && !IsHuntSoil())
            return;
        float face = FacingSign();
        float along = face * bodyRb.linearVelocity.x;
        // B-GROUND-1: with the claws on, the Walk runs at the gait's own step
        // speed — the legs have to be able to carry the advance they are drawn
        // with, and 0.7 m/s is a scurry for legs this short.
        float cap = walkSpeed;
        if (ClawGripActive() && mode == BirdMode.Walk)
            cap = Mathf.Min(cap, ClawWalkCap());
        if (along < cap)
            bodyRb.AddForce(new Vector2(walkThrust * face, 0f), ForceMode2D.Force);
        else if (IsHuntSoil() && along > walkSpeed + 0.05f)
        {
            // Bleed leftover Fly cruise on the soil film only. OnHuntDirt
            // is height < 0.45 m — that air bleed killed a 4 m/s close
            // so PeckHold never covered the Insect.
            float stanceH = bodyRb.position.y - Bird.StanceRootY(bird);
            if (stanceH < HuntSoilFilm)
                bodyRb.AddForce(new Vector2(-walkThrust * face, 0f), ForceMode2D.Force);
        }
    }

    // Ankle on the ground is not a spring to an angle: the foot is pressed, so torque goes
    // into turning the body. Only a torque against CoM offset, as on the human.
    private void DriveAnklesForBalance()
    {
        float offsetN = sensors.comOffsetX / Mathf.Max(0.01f, comOffsetReference);
        float velN = sensors.comVelocity.x / 0.4f;
        // As on the human: P·offsetN directly, without 0.06. Otherwise 0.08 N·m
        // cannot hold the gravitational moment of the head, and the bird slowly
        // rolls over the toe (bird_stand4: pitch −40° with live legs).
        float signal = Mathf.Clamp(ankleComP * offsetN + ankleComD * velN, -1f, 1f);
        ApplySignal(signal, leftAnkleFlexor, leftAnkleExtensor);
        ApplySignal(signal, rightAnkleFlexor, rightAnkleExtensor);
    }

    private void DriveNeckAndHead()
    {
        // World vertical, as the human neck: not the angle to the body, otherwise
        // the head rides with pitch and adds a lever into the dive.
        float target = headTargetTilt;
        if (mode == BirdMode.Peck)
            target = PeckNeckTarget();
        else if (mode == BirdMode.Attack)
            target = -18f;
        else if (WantsLandingGear())
            target = 8f * Mathf.Clamp(landGearAmp, 0f, 1.5f);
        else if (mode == BirdMode.Walk && (sensors.bothGrounded || OnHuntDirt()))
        {
            float hop = WalkTurnHop();
            if (hop > 0.01f)
                target = 12f * hop;
            else if (AlarmLookLeg01() > 0.01f)
                target = 0f;
            else if (walkHopAmp > 0.01f)
                target = -walkHeadBob * Mathf.Clamp(walkHopAmp, 0f, 1.5f) * WalkHopDown01()
                    - WalkScanDip();
            else if (searchCrouch > 0.01f)
                target = -22f * Mathf.Clamp01(searchCrouch);
            else
                target = walkHeadBob * Mathf.Sin((Mathf.PI * 2f) * WalkGaitPhase())
                    - WalkScanDip();
        }
        else if (mode == BirdMode.Fly && OnDirtSupport() && takeoffCrouchAmp > 0.01f)
            target = -12f * Mathf.Clamp(takeoffCrouchAmp, 0f, 1.5f);
        else
        {
            float roost = NightRoost();
            float settle = LandSettle01();
            float trail = TakeoffTrail01();
            float hop = SitTurnHop();
            float air = AirTurn01();
            if (air > 0.01f)
                target = 10f * air;
            else if (roost > 0.35f)
                target = 22f * roost;
            else if (settle > 0.01f)
                target = 8f * settle * LandSettleShake();
            else if (hop > 0.01f)
                target = 14f * hop;
            else if (trail > 0.01f)
                target = -6f * Mathf.Clamp01(trail);
            else if (AlarmLookSit01() > 0.01f)
            {
                float hunch = SitHunch01();
                if (hunch > 0.01f)
                    target = 10f * hunch;
                float startleHunch = AlarmLookSitHunch01();
                if (startleHunch > 0.01f)
                    target += 10f * startleHunch;
            }
            else if (idleHop > 0.01f && sitScratchAmp > 0.01f)
                target = 12f * idleHop * Mathf.Clamp(sitScratchAmp, 0f, 1.5f);
            else if (idlePreen > 0.01f)
                target = 22f * idlePreen;
            else if (Mathf.Abs(idleLook) > 0.01f)
            {
                target = 16f * idleLook;
                float hunch = SitHunch01();
                if (hunch > 0.01f)
                    target += 10f * hunch;
            }
            else
            {
                float hunch = SitHunch01();
                if (hunch > 0.01f)
                    target = 10f * hunch;
            }
        }
        float look = AlarmLook();
        if (look > 0.01f)
            target += 16f * look;
        float peck = PeckGape();
        float gape = CryGape();
        float stoop = StoopGape();
        float alarmGape = AlarmLookGape01();
        float walkGape = WalkScanGape01();
        float turnGape = WalkTurnGape01();
        float sitTurnGape = SitTurnGape01();
        float airTurnGape = AirTurnGape01();
        float landGape = LandGape01();
        float swoopGape = SwoopGape01();
        float takeoffGape = TakeoffGape01();
        float settleGape = LandSettleGape01();
        float preenGape = SitPreenGape01();
        float lookGape = SitLookGape01();
        float scratchGape = SitScratchGape01();
        float beakTarget = 0f;
        if (peck > 0.01f)
            beakTarget = peckGapeAmp * peck;
        else if (stoop > 0.01f)
            beakTarget = 18f * stoop;
        else if (gape > 0.01f && mode != BirdMode.Peck)
        {
            target += 12f * gape;
            beakTarget = 18f * gape;
        }
        else if (alarmGape > 0.01f)
            beakTarget = 8f * alarmGape;
        else if (walkGape > 0.01f)
            beakTarget = 8f * walkGape;
        else if (turnGape > 0.01f)
            beakTarget = 8f * turnGape;
        else if (sitTurnGape > 0.01f)
            beakTarget = 8f * sitTurnGape;
        else if (airTurnGape > 0.01f)
            beakTarget = 8f * airTurnGape;
        else if (landGape > 0.01f)
            beakTarget = 8f * landGape;
        else if (swoopGape > 0.01f)
            beakTarget = 8f * swoopGape;
        else if (takeoffGape > 0.01f)
            beakTarget = 8f * takeoffGape;
        else if (settleGape > 0.01f)
            beakTarget = 8f * settleGape;
        else if (preenGape > 0.01f)
            beakTarget = 8f * preenGape;
        else if (lookGape > 0.01f)
            beakTarget = 8f * lookGape;
        else if (scratchGape > 0.01f)
            beakTarget = 8f * scratchGape;
        ControlWorldUpright(sensors.neckTilt, sensors.neckTiltRate,
            neckJoint, neckFlexor, neckExtensor, target, ref neckI);
        ControlWorldUpright(sensors.headTilt, sensors.headTiltRate,
            headJoint, headFlexor, headExtensor, target * 0.45f, ref headI);
        if (beakJoint != null)
            ControlJointStable(beakJoint, beakTarget, beakFlexor, beakExtensor, ref beakI);
    }

    // Picture of OrganismVoice.Cry. Brain already posted Alarm; this opens
    // the beak. Not a force, not Peck (Hurt stays on BeakStrike).
    private void ApplyCryPicture()
    {
        flockBeakZ = 0f;
        float peck = PeckGape();
        if (peck > 0.01f)
        {
            flockBeakZ = peckGapeAmp * peck;
            return;
        }
        // Brain already wrote Attack (play dive / prey stoop). Talons
        // and the wing pin were live; the shut bill made BeakStrike
        // read as talons-only Fly. Held open — not a Peck Cos jab.
        float stoop = StoopGape();
        if (stoop > 0.01f)
        {
            flockBeakZ = 18f * stoop;
            return;
        }
        float gape = CryGape();
        if (gape > 0.01f)
        {
            flockBeakZ = 18f * gape;
            flockNeckZ += 12f * gape;
            flockHeadZ += 7f * gape;
        }
        else
        {
            // Brain already wrote Walk (BIRD-11). Neck, pupil, and lid
            // were live; the shut bill made dirt forage read as a
            // commute with a squint. Held part — not a Peck Cos jab.
            // Walk hop-turn reuses that part (`walkTurnGapeAmp`) —
            // WalkScanGape01 skips the window, so the about-face
            // still flashed a shut bill on the dirt drop. Sit
            // hop-turn reuses it on the perch (`sitTurnGapeAmp`) —
            // SitPreenGape01 skips that window too. Fly/Glide air
            // turn reuses it on the bank (`airTurnGapeAmp`) —
            // WalkScanGape01 / SitTurnGape01 skip Fly. Landing
            // flare reuses it on the plant (`landGapeAmp`) —
            // AirTurnGape01 skips that window. Dirt swoop reuses
            // it on the dive (`swoopGapeAmp`) — LandGape01 skips
            // that band. Takeoff climb reuses it on the hop
            // (`takeoffGapeAmp`) — SwoopGape01 skips first air.
            // Sit land-ruffle reuses it on the plant
            // (`landSettleGapeAmp`) — TakeoffGape01 skips Sit.
            // Alarm glance reuses it on Flee (`alarmLookGapeAmp`)
            // — WalkScanGape01 / AirTurnGape01 skip AlarmLook, so
            // the bill shut after the 0.55 s cry on the 1.15 s
            // glance. Sit look-down
            // reuses it on the weave (`sitLookGapeAmp`) —
            // SitLookGape01 skips hop / preen / land ruffle. Sit
            // look-down hen wattle is independently gated
            // (`sitLookWattleAmp`) — same clock, not this bill.
            // Sit idlePreen hen wattle is independently gated
            // (`sitPreenWattleAmp`) — same nibble clock, not this
            // bill.
            // Sit idleHop hen wattle is independently gated
            // (`sitScratchWattleAmp`) — same kick clock, not this
            // bill.
            // Sit hop-turn hen wattle is independently gated
            // (`sitTurnWattleAmp`) — same plant clock, not this
            // bill.
            // Walk hop-turn hen wattle is independently gated
            // (`walkTurnWattleAmp`) — same plant clock, not this
            // bill.
            // Sit
            // scratch reuses it on the kick (`sitScratchGapeAmp`) —
            // SitLookGape01 still skips idleHop.
            float alarmGape = AlarmLookGape01();
            float walk = WalkScanGape01();
            float turn = WalkTurnGape01();
            float sitTurn = SitTurnGape01();
            float airTurn = AirTurnGape01();
            float land = LandGape01();
            float swoop = SwoopGape01();
            float takeoff = TakeoffGape01();
            float settle = LandSettleGape01();
            if (alarmGape > 0.01f)
                flockBeakZ = 8f * alarmGape;
            else if (walk > 0.01f)
                flockBeakZ = 8f * walk;
            else if (turn > 0.01f)
                flockBeakZ = 8f * turn;
            else if (sitTurn > 0.01f)
                flockBeakZ = 8f * sitTurn;
            else if (airTurn > 0.01f)
                flockBeakZ = 8f * airTurn;
            else if (land > 0.01f)
                flockBeakZ = 8f * land;
            else if (swoop > 0.01f)
                flockBeakZ = 8f * swoop;
            else if (takeoff > 0.01f)
                flockBeakZ = 8f * takeoff;
            else if (settle > 0.01f)
                flockBeakZ = 8f * settle;
            else
            {
                // Brain already wrote Sit idlePreen. Wing, neck, and
                // lid were live; the shut bill made grooming a
                // squinting stretch. Nibble — not a Peck Cos jab.
                float preen = SitPreenGape01();
                if (preen > 0.01f)
                    flockBeakZ = 8f * preen;
                else
                {
                    // Brain already wrote Sit idleLook. Neck, pupil,
                    // tail, and wing weave; the shut bill made
                    // look-down a nodding statue inspecting the
                    // perch. Scale with the weave — not a call.
                    float lookGape = SitLookGape01();
                    if (lookGape > 0.01f)
                        flockBeakZ = 8f * lookGape;
                    else
                    {
                        // Brain already wrote Sit idleHop. Foot, lid,
                        // pupil, and wing were live; the shut bill
                        // made scratch a horizon kick inspecting
                        // nothing. Pulse with the kick — not a call.
                        float scratchGape = SitScratchGape01();
                        if (scratchGape > 0.01f)
                            flockBeakZ = 8f * scratchGape;
                    }
                }
            }
        }
        // Glance back through the first flee beat. Bill after cry
        // is independently gated (`alarmLookGapeAmp`); without the
        // neck Fly/Walk matches forage. Wing pin is independently
        // gated (`alarmLookWingAmp`); without it Fly still beats
        // cruise after the shout. Walk legs are independently
        // gated (`alarmLookLegAmp`); without it Walk still
        // forage-strides after the shout. Sit idle is independently
        // gated (`alarmLookSitAmp`); without it Sit still
        // look/preen/scratch after the shout. Sit startle hitch is
        // independently gated (`alarmLookSitWingAmp`); without it
        // Sit still tucks after the shout. Sit startle crouch is
        // independently gated (`alarmLookSitLegAmp`); without it
        // Sit still plants after the shout. Sit startle grasp is
        // independently gated (`alarmLookSitGraspAmp`); without it
        // Sit still plants the toes after the shout. Sit startle
        // hunch is independently gated (`alarmLookSitHunchAmp`);
        // without it Sit still holds the dirt-sit silhouette after
        // the shout. Sit startle tail is independently gated
        // (`alarmLookSitTailAmp`); without it Sit still holds the
        // pad crouch drop / dirt 6° under the Fly/Walk fan. Sit
        // startle blink is independently gated
        // (`alarmLookSitBlinkAmp`); without it Sit still forage-
        // blinks 2–5 s after the shout, including hop-turn
        // about-face. Walk startle blink is independently gated
        // (`alarmLookWalkBlinkAmp`); without it Walk still
        // forage-blinks 2–5 s after the shout, including hop-turn
        // about-face. Alarm/Call shout crop is independently gated
        // (`cryCropAmp`); without it the 0.55 s gape still left a
        // silent grey neck, so Flee flashed a mute open-bill.
        // Alarm/Call shout tongue is independently gated
        // (`cryTongueAmp`); without it the 0.55 s gape still left
        // an empty orange bill, so Flee flashed a mute swallow.
        // Alarm/Call shout hen wattle is independently gated
        // (`cryWattleAmp`); without it the 0.55 s gape still left
        // a glued throat mark, so Chicken Flee flashed a statue
        // wattle. Alarm/Call shout hen comb is independently gated
        // (`cryCombAmp`); without it the 0.55 s gape still left a
        // glued crown, so Chicken Flee flashed a statue comb.
        // Attack stoop tongue is independently gated
        // (`stoopTongueAmp`); without it the open bill still left
        // an empty orange box, so the dive flashed a mute gape.
        // Dirt-swoop tongue is independently gated
        // (`swoopTongueAmp`); without it the open bill still left
        // an empty orange box, so the dirt approach flashed a mute
        // gape.
        // Attack stoop hen wattle is independently gated
        // (`stoopWattleAmp`); without it the open bill still left
        // a glued throat mark, so Chicken Attack flashed a statue
        // wattle. Attack stoop crop is independently gated
        // (`stoopCropAmp`); without it the open bill still left
        // a silent grey neck, so the dive flashed a mute open-bill.
        // Comb stays glued on stoop (contrast). Hen Walk hop wattle is
        // independently gated (`walkWattleAmp`); without it the
        // hop still left a glued throat, so Chicken Walk flashed
        // a statue wattle. Hen Walk hop comb is independently
        // gated (`walkCombAmp`); without it the hop still left a
        // glued crown, so Chicken Walk flashed a statue comb. Sit idleLook hen wattle is independently
        // gated (`sitLookWattleAmp`); without it look-down still
        // left a glued throat, so Chicken Sit flashed a statue
        // wattle. Sit idlePreen hen wattle is independently gated
        // (`sitPreenWattleAmp`); without it the nibble still left
        // a glued throat, so Chicken Sit flashed a statue wattle.
        // Sit idleHop hen wattle is independently gated
        // (`sitScratchWattleAmp`); without it the kick still left
        // a glued throat, so Chicken Sit flashed a statue wattle.
        // Sit hop-turn hen wattle is independently gated
        // (`sitTurnWattleAmp`); without it the plant still left a
        // glued throat, so Chicken Sit flashed a statue wattle.
        // Sit land-ruffle hen wattle is independently gated
        // (`landSettleWattleAmp`); without it the plant still left a
        // glued throat, so Chicken Sit flashed a statue wattle.
        // Walk hop-turn hen wattle is independently gated
        // (`walkTurnWattleAmp`); without it the plant still left a
        // glued throat, so Chicken Walk flashed a statue wattle.
        // Walk hop-turn hen comb is independently gated
        // (`walkTurnCombAmp`); without it the plant still left a
        // glued crown, so Chicken Walk flashed a statue comb.
        // Sit hop-turn hen comb is independently gated
        // (`sitTurnCombAmp`); without it the plant still left a
        // glued crown, so Chicken Sit flashed a statue comb.
        // Sit idleLook hen comb is independently gated
        // (`sitLookCombAmp`); without it look-down still left a
        // glued crown, so Chicken Sit flashed a statue comb.
        // Sit idlePreen hen comb is independently gated
        // (`sitPreenCombAmp`); without it the nibble still left a
        // glued crown, so Chicken Sit flashed a statue comb.
        // Sit idleHop hen comb is independently gated
        // (`sitScratchCombAmp`); without it the kick still left a
        // glued crown, so Chicken Sit flashed a statue comb.
        // Sit land-ruffle hen comb is independently gated
        // (`landSettleCombAmp`); without it the plant still left a
        // glued crown, so Chicken Sit flashed a statue comb.
        // Dirt Peck swallow hen wattle is independently gated
        // (`peckWattleAmp`); without it the toss still left a
        // glued throat over a live crop, so Chicken Peck flashed a
        // statue wattle. Dirt Peck swallow hen comb is independently
        // gated (`peckCombAmp`); without it the toss still left a
        // glued crown over a live throat, so Chicken Peck flashed a
        // statue comb. Peck grab still rides the head dip.
        // Fly first-air hen wattle is independently gated
        // (`takeoffWattleAmp`); without it the hop still left a
        // glued throat over a live gape, so Chicken Fly flashed a
        // statue wattle. Fly first-air hen comb is independently
        // gated (`takeoffCombAmp`); without it the hop still left
        // a glued crown over a live throat, so Chicken Fly flashed
        // a statue comb. Fly/Glide flare hen wattle is independently
        // gated (`landWattleAmp`); without it the plant still left
        // a glued throat over a live gape, so Chicken Fly/Glide
        // flashed a statue wattle. Fly/Glide flare hen comb is
        // independently gated (`landCombAmp`); without it the plant
        // still left a glued crown over a live throat, so Chicken
        // Fly/Glide flashed a statue comb. Alarm-glance hen wattle is
        // independently gated (`alarmLookWattleAmp`); without it
        // the 1.15 s look-back still left a glued throat over a
        // live gape after the 0.55 s shout, so Chicken Flee flashed
        // a statue wattle. Alarm-glance hen comb is independently
        // gated (`alarmLookCombAmp`); without it the 1.15 s
        // look-back still left a glued crown over a live throat
        // after the 0.55 s shout, so Chicken Flee flashed a statue
        // comb. Swoop wattle / comb stay glued (hens skip swoop; crow
        // unmarked). Cry shout / Attack stoop /
        // Walk hop / Walk hop-turn / Sit look / Sit preen / Sit
        // scratch / Sit hop-turn / Sit land-ruffle / Peck swallow /
        // takeoff climb / land-flare keep their own hang. Comb nod is independently
        // gated (`cryCombAmp` shout, `alarmLookCombAmp` glance,
        // `walkCombAmp` hop,
        // `walkTurnCombAmp` dirt hop-turn, `sitTurnCombAmp` perch
        // hop-turn, `sitLookCombAmp` look-down, `sitPreenCombAmp`
        // nibble, `sitScratchCombAmp` kick, `landSettleCombAmp`
        // plant, `peckCombAmp` toss, `takeoffCombAmp` climb,
        // `landCombAmp` plant).
        // Stoop stays glued (contrast).
        float look = AlarmLook();
        if (look < 0.01f)
            return;
        flockNeckZ += 16f * look;
        flockHeadZ += 10f * look;
        ApplyAlarmLookWingPicture();
        ApplyAlarmLookSitWingPicture();
        ApplyAlarmLookSitHunchPicture();
    }

    // Hen Peck is a grab. Woodpecker Peck is already a drum (brain writes
    // the same mode); gape would read as eating the trunk. Closed bill.
    private bool IsWoodpeckerDrum()
    {
        return drumHammerHz > 0.01f
            && bird != null
            && bird.kind == BirdKind.Woodpecker;
    }

    // Dirt Peck. Base pose is sitHip*0.35 (a standing nod); the
    // brain already wrote Peck (hen forage / crow dirt Walk). Load
    // the tarsi on the down-stroke so the jab reads as a grab.
    // Swallow eases up. Pad Sit crouch and woodpecker drum keep
    // their own legs. Picture only — DriveLegs still holds ragdoll
    // pose. No Find. 0 = old standing nod.
    private void ApplyPeckLungePicture()
    {
        float u = PeckLunge01();
        if (u < 0.01f)
            return;
        float hip = sitHipAngle * (0.35f + 0.90f * u);
        flockLeftThighZ = -hip;
        flockRightThighZ = -hip * 0.92f;
        flockLeftShankZ = -12f - 16f * u;
        flockRightShankZ = -12f - 14f * u;
    }

    // Dirt eat. BeakStrike already requires FactionTable.Rel Hostile
    // and reads the HuntBeak query (not GetContacts). Brain writes Peck
    // while |insect.x - body.x| < Insect.PeckReach (0.40 m) and keeps
    // Peck through Insect.PeckHold (0.55 m) after walkThrust overshoot.
    // The strip is centred on body X and covers PeckHold both ways —
    // leftover SetFacing used to teleport a facing-biased AABB
    // (PeckHold behind / PeckHuntReach ahead) off Rel Hostile.
    // Hold the slab for the whole Peck, including under the body. Walk
    // stays walkThrust. Lift stays a flap. No Find.
    // PeckHuntReach 0.48 sits inside PeckHold 0.55, so the first Peck
    // frame is inside the trigger, not on the AABB edge. Do not raise
    // brain PeckReach to 0.48.
    // Soil film around StanceRootY. Matches the hunt foot plate. Wider
    // than the 4 cm plant so leftover Fly jitter does not re-arm cruise.
    public const float HuntSoilFilm = 0.08f;
    // Eat strip. Insect is 0.05 m tall at dirt+GroundClearance. Matching
    // 0.05 m sat on the AABB edge and missed Rel Hostile. 0.06 m plus a
    // 1 cm lift keeps the insect inside and the Ground top (y = −2)
    // outside — OverlapBox[8] must not fill with dirt before the bug.
    private const float HuntBeakHeight = 0.06f;
    private const float HuntBeakLift = 0.01f;
    // Idle park: HuntBeak Rigidbody2D.position at (0, ParkY) with
    // offset 0 in Box2D. NpcRest still reads offset ParkY — write that
    // only after simulated=false so the live fixture does not bake y=80.
    // Stays enabled so the first Peck pose is a position write, not a
    // new fixture. Above NearPerch and out of the dirt film.
    private const float HuntBeakParkY = 80f;

    private void LatchPeckReachRest()
    {
        if (bird != null && bird.beakCollider != null && !beakRestLatched)
        {
            beakRestOffset = bird.beakCollider.offset;
            beakRestSize = bird.beakCollider.size;
            beakRestLatched = true;
        }

        if (bird != null && bird.neckVisual != null && !neckRestLatched)
        {
            neckRestLocal = bird.neckVisual.localPosition;
            neckRestLatched = true;
        }
    }

    private void RestorePeckReach()
    {
        if (bird != null && bird.beakCollider != null && beakRestLatched)
        {
            bird.beakCollider.offset = beakRestOffset;
            bird.beakCollider.size = beakRestSize;
        }
        DisableHuntSlab();
        huntBeakSlabLive = false;
        huntEatLatch = false;
        huntEatHoldFixed = 0;

        if (flockRig && bird != null && bird.neckVisual != null && neckRestLatched)
            bird.neckVisual.localPosition = neckRestLocal;
    }

    // World dirt under the stance, not live body.y. Hunt Peck is legal
    // at height < 0.45 m and after a flock sink; a body-relative slab
    // sat above or below the Insect and BeakStrike never Rel Hostile.
    private float PeckDirtY()
    {
        if (bird == null)
            return -2f;
        float stand = bird.footSize.y + bird.shankSize.y
            + bird.thighSize.y + bird.bodySize.y * 0.5f;
        return Bird.StanceRootY(bird) - stand;
    }

    // 0 on a miss, a pad, or air Peck. Attack near the slab reuses
    // peckLungeAmp so a stoop can eat; 0 restores the old miss.
    private float PeckEatJab()
    {
        if (UsesStagedPeck() && peckMiss)
            return 0f;
        float jab = PeckLunge01();
        if (jab > 0.01f)
            return jab;
        if (mode != BirdMode.Attack || peckLungeAmp < 0.01f)
            return 0f;
        if (bodyRb == null)
            return 0f;
        float h = bodyRb.position.y - Bird.StanceRootY(bird);
        if (h >= 0.22f)
            return 0f;
        return Mathf.Clamp01(1f - h / 0.22f) * Mathf.Clamp(peckLungeAmp, 0f, 1.5f);
    }

    // Hunt Peck is the whole mode, not the Cos down-stroke. Staged
    // forage still uses windup/strike for the picture; eat must not
    // wait for that jab or a 1/3 PeckMiss. One probe miss must not
    // drop the slab — Insect.OnDirtPlate (height < 0.45 m, not a perch)
    // is the same dirt gate the brain uses for Walk/Peck.
    // Live HuntBeak is the eat, not the 8 m Walk. preferDirtLand used
    // to return true here and teleport a NeverSleep Dynamic Default
    // body every 200 Hz step (DirtBout flock + leftover Fly close)
    // before BeakStrike could Rel Hostile. Brain writes Peck in
    // Update (−50) before this pose (10) and BeakStrike (20). Hurt
    // still waits Rel Hostile. No GetContacts.
    private bool HuntBeakEatNow()
    {
        // Dirt hunt wins over a leftover pad Sit. perchPad used to park
        // HuntBeak at ParkY while BeakStrike OverlapCollider ran, so Rel
        // Hostile never reached Hurt. Woodpecker pad Peck (no preferDirtLand,
        // height ≥ DirtHuntHeight) still keeps the twig look-beak.
        if (mode == BirdMode.Peck)
        {
            // Woodpecker drum: pad Sit high on the trunk. Look-beak stays
            // on Head. A leftover pad after preferDirtLand flicker on the
            // dirt film must not park the first Peck frame (no latch yet)
            // — HuntBeak stayed Park Clear and Rel Hostile missed.
            // IsHuntSoil covers dirt Walk→Peck height, not preferDirtLand
            // alone.
            if (perchPad != null && !IsHuntSoil()
                && !huntEatLatch && !huntBeakSlabLive)
                return false;
            // Whole Peck is the eat. leftover treeSupport used to make
            // OnHuntDirt false and park HuntBeak while BeakStrike still
            // ran Peck on the Limb look-beak (Core×Default is off).
            return true;
        }
        return PeckEatJab() > 0.05f;
    }

    private bool WantsHuntBeakSlab()
    {
        if (HuntBeakEatNow())
        {
            huntEatLatch = true;
            huntEatHoldFixed = 2;
            return true;
        }
        return huntEatLatch;
    }

    // One decrement per physics step, not per Wants call. Update +
    // FixedUpdate + BirdFlight used to spend the extra frame before
    // Simulate, then BeakStrike OverlapCollider never Rel Hostile.
    private void TickHuntEatHold()
    {
        if (HuntBeakEatNow())
        {
            huntEatHoldFixed = 2;
            return;
        }
        if (huntEatHoldFixed > 0)
        {
            huntEatHoldFixed--;
            if (huntEatHoldFixed <= 0)
                huntEatLatch = false;
            return;
        }
        huntEatLatch = false;
    }

    private bool OnHuntDirt()
    {
        if (bodyRb == null || bird == null)
            return false;
        bool grounded = sensors != null && sensors.bothGrounded;
        // Leftover NearPerch AABB is soil plus twigs. IsHuntSoil (not
        // preferDirtLand alone) covers dirt Walk/Peck after a flicker so
        // treeSupport does not park OnDirtPlate / HuntBeak Rel Hostile.
        // Woodpecker pad drum stays high — IsHuntSoil false there.
        bool onPerch = sensors != null && sensors.treeSupport && !IsHuntSoil();
        float height = bodyRb.position.y - Bird.StanceRootY(bird);
        return Insect.OnDirtPlate(grounded, onPerch, height);
    }

    private void ApplyPeckBeakReach()
    {
        LatchPeckReachRest();
        if (bird == null)
            return;

        if (!WantsHuntBeakSlab())
        {
            if (beakRestLatched && bird.beakCollider != null)
            {
                bird.beakCollider.offset = beakRestOffset;
                bird.beakCollider.size = beakRestSize;
            }
            if (huntBeakSlabLive)
                DisableHuntSlab();
            return;
        }

        ArmHuntSlabNow();
    }

    // BeakStrike OverlapCollider reads the HuntBeak physics AABB.
    // Rigidbody2D.position is world metres (Default query body, no
    // Body compound). Brain Start already set preferDirtLand; pose here
    // so this Update (order 10) is on the insect film before BeakStrike
    // (20). Look-beak off. No SyncTransforms, no GetContacts. Simulated
    // only while live.
    private void ArmHuntSlabNow()
    {
        LatchPeckReachRest();
        EnsureHuntSlab();
        if (huntSlab == null)
            return;
        if (bird != null && bird.beakCollider != null && bird.beakCollider != huntSlab)
            bird.beakCollider.enabled = false;
        BakeHuntSlab(HuntSlabWorldPos());
        huntBeakSlabLive = true;
        BindHuntStrike(true);
    }

    private Vector2 HuntSlabWorldPos()
    {
        float bodyX = bodyRb != null ? bodyRb.position.x : transform.position.x;
        // Centre on the body. A facing-biased strip (PeckHold behind,
        // PeckHuntReach ahead) jumped when leftover SetFacing flipped
        // the nose, so OverlapCollider missed Rel Hostile on overshoot.
        // Insect.SpawnOnDirt is dirt + GroundClearance. Lift 1 cm so a
        // 0.06 m strip contains the 0.05 m bug and stays above Ground
        // top (y = −2) — OverlapCollider must see Rel Hostile first.
        float insectY = PeckDirtY() + Insect.GroundClearance + HuntBeakLift;
        return new Vector2(bodyX, insectY);
    }

    private static Vector2 HuntBeakQuerySize()
    {
        return new Vector2(Insect.PeckHold * 2f, HuntBeakHeight);
    }

    // BeakStrike OverlapCollider reads the HuntBeak physics AABB.
    // A Body-child fixture used Body (BirdCore) as the query layer, and
    // Core×Default is off, so Rel Hostile missed. HuntBeak is a world-root
    // Default query body (live Dynamic solid vs the Insect kinematic
    // trigger). Two triggers built no pair, so Rel Hostile
    // never Hurt. collider.offset at identity origin did not dirty the
    // Box2D AABB (autoSyncTransforms off, FreezeAll). Pose
    // Rigidbody2D.position; offset stays zero while live.
    // Align still zeros leftover Fly pitch on the bird
    // body (walkThrust / picture), not to tilt the strip. No GetContacts.
    private void PlaceHuntSlab(Vector2 worldPos)
    {
        if (huntSlab == null)
            return;
        if (bodyRb != null)
            AlignHuntBodyHorizon();
        ApplyHuntSlabOffset(worldPos);
    }

    private void ApplyHuntSlabOffset(Vector2 worldOffset)
    {
        ApplyHuntSlabPhysicsPose(worldOffset);
        if (huntSlab.gameObject.layer != 0)
            huntSlab.gameObject.layer = 0;
        ApplyHuntQueryLayers();
    }

    // Live eat pose. Offset-at-origin left OverlapCollider empty / ParkY.
    // Move the query body so the AABB follows walkThrust. Zero offset
    // before simulated=true so Box2D does not bake ParkY (dirt+80 miss).
    private void ApplyHuntSlabPhysicsPose(Vector2 worldPos)
    {
        if (huntSlab == null)
            return;
        if (huntSlab.offset.sqrMagnitude > 0.0000001f)
            huntSlab.offset = Vector2.zero;
        if (huntRb == null)
            huntRb = huntSlab.attachedRigidbody;
        if (huntRb != null)
        {
            huntRb.constraints = RigidbodyConstraints2D.FreezeRotation;
            huntRb.linearVelocity = Vector2.zero;
            huntRb.angularVelocity = 0f;
            if (huntRb.simulated && huntRb.IsSleeping())
                huntRb.WakeUp();
            if ((huntRb.position - worldPos).sqrMagnitude > 0.0000001f)
                huntRb.position = worldPos;
            if (Mathf.Abs(huntRb.rotation) > 0.0001f)
                huntRb.rotation = 0f;
        }

        Transform t = huntSlab.transform;
        Vector3 tp = t.position;
        t.SetPositionAndRotation(
            new Vector3(worldPos.x, worldPos.y, tp.z),
            Quaternion.identity);
    }

    // Live Peck only: Default include so BeakStrike OverlapCollider
    // reaches the Insect kinematic trigger (Rel Hostile). Park clears
    // these — see ClearHuntQueryLayers. OverlapCollider still uses the
    // matrix, which is why HuntBeak is not a Body child. Insects spawn
    // on Default. A ~exclude mask also admitted Water / UI / empty
    // layers and filled BeakStrike's 16-hit list before the bug, so Rel
    // Hostile never Hurt. Include Default only. Exclude Ground /
    // BirdLimb / BirdCore so dirt and flock torsos stay out. Masks
    // latched once — no NameToLayer in the pose loop. No GetContacts.
    private void LatchHuntQueryLayers()
    {
        if (huntQueryLayersLatched)
            return;
        huntIncludeMask = 1;
        huntExcludeMask = GroundLayers.Mask;
        BirdContactLayers.Ensure();
        int limb = LayerMask.NameToLayer(BirdContactLayers.LimbName);
        if (limb >= 0)
            huntExcludeMask |= 1 << limb;
        int core = LayerMask.NameToLayer(BirdContactLayers.CoreName);
        if (core >= 0)
            huntExcludeMask |= 1 << core;
        huntQueryLayersLatched = true;
    }

    private void ApplyHuntQueryLayers()
    {
        if (huntSlab == null)
            return;
        LatchHuntQueryLayers();
        if (huntSlab.includeLayers.value != huntIncludeMask)
            huntSlab.includeLayers = huntIncludeMask;
        if (huntSlab.excludeLayers.value != huntExcludeMask)
            huntSlab.excludeLayers = huntExcludeMask;
        if (huntSlab.layerOverridePriority < 1)
            huntSlab.layerOverridePriority = 1;
        if (huntRb == null)
            huntRb = huntSlab.attachedRigidbody;
        if (huntRb == null)
            return;
        // Unity 6 ANDs Rigidbody2D include/exclude with the collider.
        // Default 0 (Nothing) on the body dropped Default×Default, so
        // Rel Hostile never reached the Insect trigger. bodyType /
        // isTrigger / simulated flips zero the RB mask — re-latch
        // after every live flip (BakeHuntSlab / PrepareHuntQueryBody).
        if (huntRb.includeLayers.value != huntIncludeMask)
            huntRb.includeLayers = huntIncludeMask;
        if (huntRb.excludeLayers.value != huntExcludeMask)
            huntRb.excludeLayers = huntExcludeMask;
        // layerOverridePriority is Collider2D only (CS1061 on
        // Rigidbody2D). Masks AND with the Insect kinematic trigger.
        // No GetContacts.
    }

    // Park belt, not live Peck Rel Hostile. Unity 6 ANDs RB
    // include/exclude with the collider; leftover Default include
    // after an already-Kinematic park keeps Default on a bodyType
    // no-op, so a simulated leak is Default×Default on the flock
    // island (BIRD-12). Include Nothing / exclude all while parked.
    // BakeHuntSlab / PrepareHuntQueryBody restore Default on live
    // Peck so Rel Hostile can Hurt. No NameToLayer (0 / AllLayers).
    private void ClearHuntQueryLayers()
    {
        if (huntSlab != null)
        {
            if (huntSlab.includeLayers.value != 0)
                huntSlab.includeLayers = 0;
            if (huntSlab.excludeLayers.value != Physics2D.AllLayers)
                huntSlab.excludeLayers = Physics2D.AllLayers;
        }

        if (huntRb == null && huntSlab != null)
            huntRb = huntSlab.attachedRigidbody;
        if (huntRb == null)
            return;
        if (huntRb.includeLayers.value != 0)
            huntRb.includeLayers = 0;
        if (huntRb.excludeLayers.value != Physics2D.AllLayers)
            huntRb.excludeLayers = Physics2D.AllLayers;
    }

    // Pose snap, not a force. Sleeping leftover Sit kept pitch until Walk
    // WakeUp. HuntBeak is world-axis; leftover Fly rotation still yanks
    // walkThrust off ±X. Zero RB rotation (immediate) and copy it onto
    // the Body Transform this Update. Not a flap.
    private void AlignHuntBodyHorizon()
    {
        if (bodyRb == null)
            return;
        if (bodyRb.IsSleeping())
            bodyRb.WakeUp();
        bodyRb.angularVelocity = 0f;
        if (Mathf.Abs(bodyRb.rotation) > 0.0001f)
            bodyRb.rotation = 0f;
        Transform bodyT = bodyRb.transform;
        Vector2 pose = bodyRb.position;
        Vector3 tp = bodyT.position;
        bodyT.SetPositionAndRotation(
            new Vector3(pose.x, pose.y, tp.z),
            Quaternion.identity);
    }

    // BeakStrike OverlapCollider uses the HuntBeak physics AABB —
    // dirt Rigidbody2D.position this Update, not ParkY leftover. Do not
    // disable the collider: that drops it from the broadphase and the
    // next enable is the 1×1-at-origin miss. simulated was off while
    // parked (flock 200 Hz). Pose Dynamic solid, offset 0 at dirt
    // while still unsim, then simulated=true so Box2D bakes the insect
    // film, not ParkY or a trigger fixture. No SyncTransforms, no
    // GetContacts. Dynamic solid only while live.
    private void BakeHuntSlab(Vector2 worldPos)
    {
        if (huntSlab == null)
            return;
        if (huntRb == null)
            huntRb = huntSlab.attachedRigidbody;
        bool wasSim = huntRb != null && huntRb.simulated;
        if (!HuntSlabFixtureReady())
            ApplyHuntSlabFixture();
        if (!huntSlab.enabled)
            huntSlab.enabled = true;
        if (!wasSim)
        {
            // Park is Kinematic trigger + FreezeAll at ParkY. A dirt
            // write while that freeze still held left OverlapCollider
            // at y=80. Flipping Dynamic+solid only as simulated=true
            // rebuilt the fixture from the trigger and zeroed Unity 6
            // includeLayers (Nothing), so Default×Default dropped and
            // Rel Hostile never Hurt. Pose the live body while unsim.
            if (huntRb != null)
            {
                if (huntRb.bodyType != RigidbodyType2D.Dynamic)
                    huntRb.bodyType = RigidbodyType2D.Dynamic;
                huntRb.constraints = RigidbodyConstraints2D.FreezeRotation;
                huntRb.linearVelocity = Vector2.zero;
                huntRb.angularVelocity = 0f;
            }
            if (huntSlab.isTrigger)
                huntSlab.isTrigger = false;
            if (huntSlab.offset.sqrMagnitude > 0.0000001f)
                huntSlab.offset = Vector2.zero;
            PlaceHuntSlab(worldPos);
            ApplyHuntQueryLayers();
            PrepareHuntQueryBody();
            if (huntRb != null && huntRb.IsSleeping())
                huntRb.WakeUp();
            PlaceHuntSlab(worldPos);
            // Do not disable/enable here: that was the 1×1-at-origin
            // miss (fixture left the broadphase). The pair exists after
            // the next Simulate; huntEatHold keeps Peck live until then.
            return;
        }

        if (huntRb != null
            && huntRb.constraints != RigidbodyConstraints2D.FreezeRotation)
            huntRb.constraints = RigidbodyConstraints2D.FreezeRotation;
        PrepareHuntQueryBody();
        PlaceHuntSlab(worldPos);
    }

    private bool HuntSlabFixtureReady()
    {
        if (huntSlab == null)
            return false;
        Vector2 want = HuntBeakQuerySize();
        return Mathf.Abs(huntSlab.size.x - want.x) < 0.0001f
            && Mathf.Abs(huntSlab.size.y - want.y) < 0.0001f;
    }

    // Call from FixedUpdate before Simulate, and from BirdFlight after a
    // hunt Y snap. Waiting for huntBeakSlabLive (set in Update) left the
    // first Peck OverlapCollider at ParkY, so Rel Hostile never reached Hurt.
    // Pose is Rigidbody2D.position (physics AABB), not collider.offset.
    public void RefreshHuntBeakPose()
    {
        PoseLiveHuntSlab();
    }

    private void PoseLiveHuntSlab()
    {
        if (energyRest)
            return;
        if (!WantsHuntBeakSlab())
            return;
        ArmHuntSlabNow();
    }

    private bool HuntBeakIsWorldQuery()
    {
        if (huntSlab == null)
            return false;
        Transform t = huntSlab.transform;
        if (t == null || t.parent != null)
            return false;
        if (huntSlab.gameObject.layer != 0)
            return false;
        if (huntRb == null)
            huntRb = huntSlab.attachedRigidbody;
        // Keep the GO even while parked (simulated=false). Destroying
        // an unsim leftover (NpcRest rest) orphaned the Bind cache and
        // the next Peck OverlapCollider ran on a fresh 1×1. Live eat is
        // Dynamic solid × Insect kinematic trigger. Park unsims so
        // leftover Sit / hunt Walk is not a 200 Hz island. Recreate
        // only if the RB is missing.
        return huntRb != null;
    }

    // OverlapCollider is silent on Static×Static and on two trigger
    // sensors (HuntBeak × Insect) even with useFullKinematicContacts
    // and a Dynamic HuntBeak trigger. Insect is a FreezeAll kinematic
    // trigger with the flag and NeverSleep. Live eat is Dynamic solid
    // × that Insect trigger so Rel Hostile reaches Hurt. Walk stays
    // parked unsim — a live Dynamic on the 8 m close was the flock
    // 200 Hz island (BIRD-12). FreezeRotation only — FreezeAll pinned
    // the AABB at the origin so Rel Hostile never reached Hurt.
    // simulated only while the slab is live. Do not Destroy.
    private void PrepareHuntQueryBody()
    {
        if (huntSlab == null)
            return;
        if (huntRb == null)
            huntRb = huntSlab.attachedRigidbody;
        if (huntRb == null)
            return;
        // Dynamic solid × Insect kinematic trigger is the
        // OverlapCollider pair. Two triggers returned 0 hits.
        if (huntRb.bodyType != RigidbodyType2D.Dynamic)
            huntRb.bodyType = RigidbodyType2D.Dynamic;
        if (huntSlab.isTrigger)
            huntSlab.isTrigger = false;
        if (huntRb.constraints != RigidbodyConstraints2D.FreezeRotation)
            huntRb.constraints = RigidbodyConstraints2D.FreezeRotation;
        huntRb.gravityScale = 0f;
        huntRb.interpolation = RigidbodyInterpolation2D.None;
        huntRb.collisionDetectionMode = CollisionDetectionMode2D.Discrete;
        huntRb.sleepMode = RigidbodySleepMode2D.NeverSleep;
        // Caller poses offset 0 at dirt first when waking. Flipping
        // simulated here bakes that pose; doing it before PlaceHuntSlab
        // baked ParkY and Rel Hostile missed.
        if (!huntRb.simulated)
        {
            huntRb.simulated = true;
            // Park FreezeAll + unsim leaves the body asleep.
            // OverlapCollider on a sleeping Default query is
            // empty, so Rel Hostile never reached Hurt on the first
            // Peck Simulate. Wake on enter — IsSleeping can still be
            // true the same step simulated flipped. No GetContacts.
            huntRb.WakeUp();
        }
        else if (huntRb.IsSleeping())
            huntRb.WakeUp();
        // Unity 6 ANDs Rigidbody2D include/exclude with the collider.
        // bodyType / isTrigger rebuilds the b2Body and the RB mask
        // goes back to Nothing (0). Default×Default then drops, so
        // BeakStrike OverlapCollider never Rel Hostile. Re-latch
        // after every live flip. No GetContacts.
        ApplyHuntQueryLayers();
    }

    private void EnsureHuntSlab()
    {
        if (HuntBeakIsWorldQuery())
            return;
        if (huntSlab != null)
        {
            Transform old = huntSlab.transform;
            huntSlab = null;
            huntRb = null;
            if (old != null && (bodyRb == null || old.gameObject != bodyRb.gameObject))
                Destroy(old.gameObject);
        }

        if (bodyRb == null)
            return;

        // World-root Default query (BIRD-12 flock 200 Hz: park unsims,
        // no GetContacts). Add the Rigidbody2D before the collider so
        // OverlapCollider has an attached body. No RB then Static×Static
        // then two kinematic sensors all returned 0 hits, so Rel
        // Hostile never reached Hurt. Live body is Dynamic solid vs
        // the Insect kinematic trigger. Birth stays Kinematic unsim —
        // Prepare flips Dynamic only while Peck/Attack. Pose is
        // Rigidbody2D.position. Parenting to Body used BirdCore;
        // Core×Default is off.
        GameObject go = new GameObject("HuntBeak");
        go.layer = 0;
        go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        huntRb = go.AddComponent<Rigidbody2D>();
        huntRb.bodyType = RigidbodyType2D.Kinematic;
        huntRb.useFullKinematicContacts = true;
        huntRb.constraints = RigidbodyConstraints2D.FreezeRotation;
        huntRb.gravityScale = 0f;
        // Unsim from birth (BIRD-12 flock 200 Hz). simulated=true at
        // origin put a Default query in the first broadphase; BakeHunt
        // parks next, but a leftover origin AABB made OverlapCollider
        // miss Rel Hostile on the first Peck. Wake poses dirt first.
        huntRb.simulated = false;
        huntRb.interpolation = RigidbodyInterpolation2D.None;
        huntRb.collisionDetectionMode = CollisionDetectionMode2D.Discrete;
        // Park immediately; NeverSleep only while Peck/Attack is live
        // (PrepareHuntQueryBody). Birth NeverSleep + a stray simulated
        // at origin was a 200 Hz Default island (BIRD-12).
        huntRb.sleepMode = RigidbodySleepMode2D.StartAwake;
        huntSlab = go.AddComponent<BoxCollider2D>();
        huntSlab.isTrigger = true;
        // Thin on Y: Ground top is y = −2; Insect centre is dirt + 0.04.
        // A 0.12 m strip overlapped Ground and filled OverlapBox[8].
        // X is PeckHold both ways so leftover SetFacing cannot teleport
        // the query off Rel Hostile. Park is offset (0, ParkY) at origin.
        ApplyHuntSlabFixture();
        huntSlab.enabled = true;
        ParkHuntSlab();
        if (huntStrike == null)
            huntStrike = GetComponent<BeakStrike>();
    }

    // Size only. Do not ApplyHuntQueryLayers here: EnsureHuntSlab calls
    // this before Park, and Default include on an already-Kinematic /
    // unsim park body survives a missed Clear as Default×Default on the
    // flock island (BIRD-12, 200 Hz). Live Peck restores Default in
    // BakeHuntSlab / PrepareHuntQueryBody / PlaceHuntSlab so BeakStrike
    // OverlapCollider can Rel Hostile. Park is a trigger; live Prepare
    // flips to a solid vs the Insect kinematic trigger. PlaceHuntSlab
    // writes Rigidbody2D.position. No GetContacts.
    private void ApplyHuntSlabFixture()
    {
        if (huntSlab == null)
            return;
        huntSlab.size = HuntBeakQuerySize();
        lastHuntSize = huntSlab.size;
    }

    private void BindHuntStrike(bool onSlab)
    {
        if (huntStrike == null)
            huntStrike = GetComponent<BeakStrike>();
        if (huntStrike == null)
            return;
        if (onSlab && huntSlab != null)
        {
            huntStrike.Bind(huntSlab);
            huntStrikeOnSlab = true;
            return;
        }
        if (huntStrikeOnSlab && bird != null && bird.beakCollider != null)
            huntStrike.Bind(bird.beakCollider);
        huntStrikeOnSlab = false;
    }

    private void DisableHuntSlab()
    {
        // Park, do not disable: dropping the fixture from Simulate made
        // the next Peck read a 1×1 at the origin (Rel Hostile missed).
        ParkHuntSlab();
        huntBeakSlabLive = false;
        huntEatLatch = false;
        huntEatHoldFixed = 0;
        if (bird != null && bird.beakCollider != null)
            bird.beakCollider.enabled = true;
        BindHuntStrike(false);
    }

    private void ParkHuntSlab()
    {
        if (huntSlab == null)
            return;
        if (huntRb == null)
            huntRb = huntSlab.attachedRigidbody;
        Vector2 park = new Vector2(0f, HuntBeakParkY);
        // Unsim before ParkY offset. Writing offset while simulated baked
        // y=80 into Box2D; the next Peck then OverlapCollider-missed the
        // Insect (Rel Hostile never Hurt) even after Rigidbody2D.position
        // sat on dirt. Physics park is Rigidbody2D.position. NpcRest still
        // reads offset ParkY — C# only, after the body left Simulate.
        if (huntRb != null && huntRb.simulated)
            huntRb.simulated = false;
        if (huntRb != null)
        {
            // Park is Kinematic unsim. Live Peck is Dynamic — leave
            // that body type in Simulate and the flock 200 Hz island
            // comes back (BIRD-12).
            if (huntRb.bodyType != RigidbodyType2D.Kinematic)
                huntRb.bodyType = RigidbodyType2D.Kinematic;
            huntRb.useFullKinematicContacts = true;
            // Park is unsim. StartAwake so a leftover simulated=true at
            // ParkY is not a NeverSleep 200 Hz island. Live eat still
            // NeverSleep in PrepareHuntQueryBody.
            huntRb.sleepMode = RigidbodySleepMode2D.StartAwake;
            huntRb.constraints = RigidbodyConstraints2D.FreezeAll;
            huntRb.position = park;
            huntRb.rotation = 0f;
            huntRb.linearVelocity = Vector2.zero;
            huntRb.angularVelocity = 0f;
        }

        if (!huntSlab.isTrigger)
            huntSlab.isTrigger = true;

        huntSlab.transform.SetPositionAndRotation(
            new Vector3(park.x, park.y, huntSlab.transform.position.z),
            Quaternion.identity);
        if ((huntSlab.offset - park).sqrMagnitude > 0.0000001f)
            huntSlab.offset = park;
        if (huntSlab.gameObject.layer != 0)
            huntSlab.gameObject.layer = 0;
        // Do not ApplyHuntQueryLayers here: Default include on an
        // already-Kinematic park survives the bodyType no-op, so a
        // simulated leak is Default×Default at 200 Hz (BIRD-12).
        // Live Peck re-latches Default in Bake / Prepare. Rel Hostile
        // still needs that restore — Clear is park only.
        ClearHuntQueryLayers();
        if (!huntSlab.enabled)
            huntSlab.enabled = true;
    }

    // Visible jab. The query already sits on the slab; this only
    // drops the flock neck so the bill is not a hovering Cos nod.
    private void ApplyPeckNeckReachPicture()
    {
        if (!flockRig || bird == null || bird.neckVisual == null)
            return;
        LatchPeckReachRest();
        if (!neckRestLatched)
            return;
        float jab = PeckEatJab();
        if (jab < 0.01f)
        {
            bird.neckVisual.localPosition = neckRestLocal;
            return;
        }

        float drop = Bird.PeckNeckDropKnob >= 0f ? Bird.PeckNeckDropKnob : peckNeckDropM;
        bird.neckVisual.localPosition = neckRestLocal
            + new Vector3(peckReachAhead * jab, -drop * jab, 0f);
    }

    // B-PERCH-27: the body's own lowering at the jab (see peckBodyDropM). Picture only:
    // the drawn torso and the whole upper body sink by the dose while the legs' roots ride
    // up by the same amount, so the feet, the claws and every collider keep their world
    // pose; the physical root never moves. Dose 0 = the old picture exactly.
    private void LatchPeckBodyDropRest()
    {
        if (bodyDropRestLatched || bird == null || !flockRig)
            return;
        bodyDropRestLatched = true;
        if (bird.lookRoot != null)
        {
            bodyDropLookRest = bird.lookRoot.localPosition;
            bodyDropHasLook = true;
        }
        if (bird.leftThigh != null && bird.rightThigh != null
            && bird.leftThigh.parent == bird.lookRoot)
        {
            bodyDropThighRestL = bird.leftThigh.localPosition;
            bodyDropThighRestR = bird.rightThigh.localPosition;
            bodyDropHasLegs = true;
        }
        if (bird.bodyCollider != null)
        {
            HumanSegment seg = bird.bodyCollider.GetComponent<HumanSegment>();
            bodyDropTorso = seg != null ? seg.visual : null;
            if (bodyDropTorso != null)
            {
                bodyDropTorsoRest = bodyDropTorso.localPosition;
                bodyDropHasTorso = true;
            }
        }
    }

    private void ApplyPeckBodyDropPicture()
    {
        if (!flockRig || bird == null)
            return;
        LatchPeckBodyDropRest();
        if (!bodyDropHasLook && !bodyDropHasLegs && !bodyDropHasTorso)
            return;
        float dose = Bird.PeckBodyDropKnob >= 0f ? Bird.PeckBodyDropKnob : peckBodyDropM;
        float drop = dose > 0.0005f ? dose * PeckEatJab() : 0f;
        Vector3 down = new Vector3(0f, -drop, 0f);
        Vector3 up = new Vector3(0f, drop, 0f);
        if (bodyDropHasLook)
            bird.lookRoot.localPosition = bodyDropLookRest + down;
        if (bodyDropHasTorso && bodyDropTorso != null)
            bodyDropTorso.localPosition = bodyDropTorsoRest + down;
        if (bodyDropHasLegs && bird.leftThigh != null && bird.rightThigh != null)
        {
            bird.leftThigh.localPosition = bodyDropThighRestL + up;
            bird.rightThigh.localPosition = bodyDropThighRestR + up;
        }
    }

    // Hen/crow Peck: brace both wings on the jab. Picture only —
    // DriveWings still holds ragdoll pose. Lift stays in BirdFlight.
    // Pad hunch already tucked; this opens from that fold. No Find.
    // 0 = old Sit-adjacent tuck (peckWingAmp 0 or woodpecker drum).
    private void ApplyPeckWingPicture()
    {
        float flick = PeckWingFlick();
        if (flick < 0.01f)
            return;
        // Plus from −90 lifts the box off the back (takeoff cock),
        // not preen (more-negative, one wing to the beak). Add on
        // the current fold so pad hunch stays the base tuck.
        flockLeftWingFold += 16f * flick;
        flockRightWingFold += 14f * flick;
        flockLeftUlnaZ = Mathf.Lerp(flockLeftUlnaZ, groundFoldUlnaZ + groundFlickLiftDeg - 2f, Mathf.Clamp01(flick));
        flockRightUlnaZ = Mathf.Lerp(flockRightUlnaZ, groundFoldUlnaZ + groundFlickLiftDeg - 4f, Mathf.Clamp01(flick));
    }

    // 0 swallow / air / drum / off, peak mid-jab. Same Cos as gape.
    // Includes peckWingAmp.
    private float PeckWingFlick()
    {
        if (peckWingAmp < 0.01f || mode != BirdMode.Peck)
            return 0f;
        if (IsWoodpeckerDrum())
            return 0f;
        if (sensors != null && !sensors.bothGrounded && !OnHuntDirt())
            return 0f;
        return PeckDown01() * Mathf.Clamp(peckWingAmp, 0f, 1.5f);
    }

    // Hen/crow Peck: drop the free end on the jab. Picture only —
    // DriveTail still holds ragdoll pitch. Woodpecker drum keeps
    // its own fan. Swallow keeps the Sit drop. No Find.
    // 0 = old Sit drop (peckTailAmp 0 or woodpecker drum).
    private float PeckTail01()
    {
        if (peckTailAmp < 0.01f || mode != BirdMode.Peck)
            return 0f;
        if (IsWoodpeckerDrum())
            return 0f;
        if (sensors != null && !sensors.bothGrounded && !OnHuntDirt())
            return 0f;
        return PeckDown01() * Mathf.Clamp(peckTailAmp, 0f, 1.5f);
    }

    // 0 on a pad, in air, or when the lever is off. Peak on the
    // same Cos as gape (down), zero on the swallow toss.
    private float PeckLunge01()
    {
        if (peckLungeAmp < 0.01f || mode != BirdMode.Peck)
            return 0f;
        if (IsWoodpeckerDrum())
            return 0f;
        if (perchPad != null)
            return 0f;
        if (sensors != null && !sensors.bothGrounded && !OnHuntDirt())
            return 0f;
        return PeckDown01() * Mathf.Clamp(peckLungeAmp, 0f, 1.5f);
    }

    // Flock Peck head. Hen: Cos dip. Crow dirt: staged windup /
    // strike / head-up from BirdWander. Woodpecker: 12 Hz chisel.
    // Picture only — BeakStrike still reads the trigger. No Find.
    private void ApplyPeckHeadPicture()
    {
        if (IsWoodpeckerDrum())
        {
            float w = (Mathf.PI * 2f) * drumHammerHz * Time.fixedTime;
            float stroke = 0.5f + 0.5f * Mathf.Sin(w);
            flockNeckZ = -drumRestDip - drumHammerAmp * stroke;
            flockHeadZ = flockNeckZ * 0.15f;
            return;
        }

        if (UsesStagedPeck())
        {
            flockNeckZ = PeckNeckTarget();
            flockHeadZ = flockNeckZ * 0.35f;
            return;
        }

        float bob = 7f * Mathf.Sin(Time.fixedTime * 14f);
        float dip = -peckDipDegrees + bob;
        float u = PeckSwallow01();
        flockNeckZ = u > 0.01f ? Mathf.Lerp(dip, 18f * PeckSwallowScale(), u) : dip;
        flockHeadZ = flockNeckZ * 0.35f;
    }

    // Peck already dips the neck; without this the mouth stays shut and
    // forage Walk/Peck reads as a nodding Sit. Same 14 rad/s as the flock
    // neck bob: open while the head drops, shut on the way up. Not Hurt.
    private float PeckGape()
    {
        if (mode != BirdMode.Peck || peckGapeAmp < 0.01f)
            return 0f;
        if (IsWoodpeckerDrum())
            return 0f;
        if (sensors != null && !sensors.bothGrounded && !OnHuntDirt())
            return 0f;
        if (UsesStagedPeck() && peckMiss)
            return 0f;
        return PeckDown01();
    }

    // Brain already wrote Peck. Down is grab (gape + dip); without this
    // the up-stroke stays at −peckDip and forage eat reads as a stuck nod.
    // Closed bill — PeckGape is already 0 on Cos > 0. Not Hurt, not a toss force.
    private float PeckSwallow01()
    {
        if (peckSwallowAmp < 0.01f || mode != BirdMode.Peck)
            return 0f;
        if (IsWoodpeckerDrum())
            return 0f;
        if (sensors != null && !sensors.bothGrounded && !OnHuntDirt())
            return 0f;
        return PeckUp01();
    }

    private float PeckSwallowScale()
    {
        return Mathf.Clamp(peckSwallowAmp, 0f, 1.5f);
    }

    // Same 14 rad/s as the flock peck bob / gape. No Find.
    private static float PeckCycleCos()
    {
        return Mathf.Cos(Time.fixedTime * 14f);
    }

    // Crow dirt forage: BirdWander stages windup / strike / head-up.
    // Hens and Cos pecks stay on PeckCycleCos. Not Hurt.
    private bool UsesStagedPeck()
    {
        return peckWindup > 0.01f || peckStrike > 0.01f || peckHeadUp > 0.01f;
    }

    private float PeckDown01()
    {
        if (UsesStagedPeck())
        {
            if (peckStrike < 0.01f)
                return 0f;
            return peckStrike * (peckMiss ? 0.42f : 1f);
        }

        float c = PeckCycleCos();
        return c < 0f ? -c : 0f;
    }

    private float PeckUp01()
    {
        if (UsesStagedPeck())
        {
            if (peckMiss || peckHeadUp < 0.01f)
                return 0f;
            return peckHeadUp;
        }

        float c = PeckCycleCos();
        return c > 0f ? c : 0f;
    }

    // Ragdoll Peck neck. Drum stays the brace — do not chase 12 Hz.
    // Crow dirt: pause, jab, then head-up. Swallow is ~2.2 Hz Cos
    // on hens. 0 = old dip.
    private float PeckNeckTarget()
    {
        if (IsWoodpeckerDrum())
            return -drumRestDip;
        if (UsesStagedPeck())
        {
            if (peckHeadUp > 0.01f)
                return 22f * peckHeadUp;
            if (peckStrike > 0.01f)
                return -peckDipDegrees * peckStrike * (peckMiss ? 0.42f : 1f);
            if (peckWindup > 0.01f)
                return -10f * peckWindup;
            return 0f;
        }

        float u = PeckSwallow01();
        if (u < 0.01f)
            return -peckDipDegrees;
        return Mathf.Lerp(-peckDipDegrees, 18f * PeckSwallowScale(), u);
    }

    private float CryGape()
    {
        EnsureVoice();
        if (voice == null)
            return 0f;
        if (voice.LastKind != VoiceKind.Alarm && voice.LastKind != VoiceKind.Call)
            return 0f;
        float age = Time.time - voice.LastCryTime;
        if (age < 0f || age > 0.55f)
            return 0f;
        if (age < 0.08f)
            return age / 0.08f;
        if (age < 0.32f)
            return 1f;
        return 1f - (age - 0.32f) / 0.23f;
    }

    // Picture of BIRD-6 Flee. Brain already cried and wrote Fly (hen: Walk).
    // Gape lasts 0.55 s; this holds a look-back through the first flee
    // stroke. Cached voice, no Find. 0 = beak-only cry. Not a force.
    private float AlarmLook()
    {
        if (alarmLookAmp < 0.01f)
            return 0f;
        if (mode == BirdMode.Attack || mode == BirdMode.Peck || mode == BirdMode.Glide)
            return 0f;
        if (mode == BirdMode.Fly && OnDirtSupport())
            return 0f;
        if (TakeoffWing01() > 0.01f || TakeoffTrail01() > 0.01f)
            return 0f;
        EnsureVoice();
        if (voice == null)
            return 0f;
        if (voice.LastKind != VoiceKind.Alarm && voice.LastKind != VoiceKind.Call)
            return 0f;
        float age = Time.time - voice.LastCryTime;
        if (age < 0f || age > 1.15f)
            return 0f;
        float a = Mathf.Clamp(alarmLookAmp, 0f, 1.5f);
        if (age < 0.12f)
            return a * (age / 0.12f);
        if (age < 0.70f)
            return a;
        return a * (1f - (age - 0.70f) / 0.45f);
    }

    private void EnsureVoice()
    {
        if (voice != null || voiceMisses >= 4)
            return;
        float now = Time.time;
        if (now < nextVoiceLookup)
            return;
        nextVoiceLookup = now + 0.5f;
        voice = GetComponent<OrganismVoice>();
        if (voice == null)
            voiceMisses++;
    }

    private void DriveTail(bool flying)
    {
        if (tailJoint == null || sensors == null) return;

        // Tail on the child segment, body is connectedBody, as thigh and pelvis:
        // a positive signal turns the body nose-up. The goal is a small
        // positive pitch so the flap carries forward-up, not into a loop.
        float target = flying ? pitchTarget : 4f;
        float p = flying ? pitchPGain : 1.6f;
        float error = Mathf.DeltaAngle(sensors.bodyPitch, target) / Mathf.Max(1f, pitchErrorRef);
        float speed = sensors.bodyPitchRate / Mathf.Max(1f, speedReferenceDegPerSec);
        float raw = p * error - pitchDGain * speed;
        ApplySignal(Mathf.Clamp(raw, -1f, 1f), tailFlexor, tailExtensor);
    }

    private void ControlWorldUpright(float tilt, float angVel, HingeJoint2D joint,
                                     Muscle flexor, Muscle extensor, float target, ref float cachedI)
    {
        if (joint == null || flexor == null || extensor == null) return;

        float dt = Time.fixedDeltaTime;
        float tiltForError = tilt + angVel * dt;
        if (cachedI <= 0f)
            cachedI = EffectiveInertia(joint);

        float damping = 0f;
        if (cachedI > 0f)
        {
            float k = jointDGain * flexor.maxTorque * Mathf.Rad2Deg
                      / Mathf.Max(1f, speedReferenceDegPerSec);
            damping = k * dt / cachedI;
        }

        float error = Mathf.DeltaAngle(tiltForError, target) / Mathf.Max(1f, jointErrorRef);
        float spd = angVel / Mathf.Max(1f, speedReferenceDegPerSec);
        // Joint lives on the segment itself — sign as the human lumbar: −P, −D.
        float raw = (-jointPGain) * error - (-jointDGain) * spd;
        ApplySignal(Mathf.Clamp(raw / (1f + damping), -1f, 1f), flexor, extensor);
    }

    private void ControlJointStable(HingeJoint2D joint, float targetAngle,
                                    Muscle flexor, Muscle extensor, ref float cachedI)
    {
        if (joint == null || flexor == null || extensor == null) return;

        float dt = Time.fixedDeltaTime;
        float angleForError = joint.jointAngle + joint.jointSpeed * dt;
        if (cachedI <= 0f)
            cachedI = EffectiveInertia(joint);

        float damping = 0f;
        if (cachedI > 0f)
        {
            float k = jointDGain * flexor.maxTorque * Mathf.Rad2Deg
                      / Mathf.Max(1f, speedReferenceDegPerSec);
            damping = k * dt / cachedI;
        }

        float error = Mathf.DeltaAngle(angleForError, targetAngle) / Mathf.Max(1f, jointErrorRef);
        float speed = joint.jointSpeed / speedReferenceDegPerSec;
        float raw = jointPGain * error - jointDGain * speed;
        ApplySignal(Mathf.Clamp(raw / (1f + damping), -1f, 1f), flexor, extensor);
    }

    // A positive signal increases jointAngle; that is the extensor.
    private static void ApplySignal(float signal, Muscle flexor, Muscle extensor)
    {
        if (signal > 0f)
        {
            UpdateMuscle(extensor, signal);
            UpdateMuscle(flexor, 0f);
        }
        else
        {
            UpdateMuscle(extensor, 0f);
            UpdateMuscle(flexor, -signal);
        }
    }

    private static void UpdateMuscle(Muscle muscle, float activation)
    {
        if (muscle != null)
            muscle.activation = Mathf.Clamp01(activation);
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

    private void SumDebugLift()
    {
        debugLiftY = flight != null ? flight.lastLiftY : 0f;
    }
}
