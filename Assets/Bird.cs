using System.Collections.Generic;
using UnityEngine;

public enum BirdRig
{
    // Stand: joints and muscles, as a stance check.
    Ragdoll = 0,
    // Scene: one body. Wings and feet are a picture. Tens of individuals.
    Flock = 1
}

// Species recipe. Spawners may still AddComponent<Bird>() and set kind;
// Crow / Chicken / Woodpecker override the same hooks when attached as themselves.
public enum BirdKind
{
    Crow = 0,
    Chicken = 1,
    Woodpecker = 2
}

// How the brain forage loop is built. Body data, not a force.
public enum BirdForageStyle
{
    CrowCruise = 0,
    ChickenGround = 1,
    WoodpeckerDrum = 2
}

// Build a segmental bird. Bird body layer, not the human.
// The segment is HumanSegment: a shared 2D primitive (box + Visual);
// it does not contain human anatomy. Muscles and friction are the same actuators.
// Species layout, flight, and control live on Crow / Chicken / Woodpecker.
public class Bird : MonoBehaviour
{
    [Header("Total mass, kg")]
    public float totalMass = 0.5f;
    [Tooltip("Crow — flight. Chicken — ground, a short hop.")]
    public BirdKind kind = BirdKind.Crow;
    [Tooltip("Flock — one body for the flock. Ragdoll — stance stand only.")]
    public BirdRig rig = BirdRig.Flock;

    public BirdSensors sensors;
    public BirdController controller;
    public BirdFlight flight;
    public HumanSegment headSegment;
    public HumanSegment beakSegment;
    public Transform headVisual;
    public Transform neckVisual;
    public Transform beakVisual;
    public BoxCollider2D beakCollider;
    public BoxCollider2D bodyCollider;
    public BoxCollider2D headCollider;
    public BoxCollider2D perchCollider;
    public Transform leftWingRoot;
    public Transform rightWingRoot;
    public Transform leftUlna;
    public Transform rightUlna;
    // B-WING-3: the wing's outer segment (the wrist joint of the picture).
    // Bound for both rigs — the flock rig's picture chain and the recorder's
    // own wing readings.
    public Transform leftHand;
    public Transform rightHand;
    // B-WING-4: the spread wing's area — the feather fan bound to the hand
    // (primaries) and the ulna (secondaries). Picture only; null until the
    // flock rig builds it.
    public WingFeatherFan leftFan;
    public WingFeatherFan rightFan;
    public Transform leftThigh;
    public Transform rightThigh;
    public Transform leftShank;
    public Transform rightShank;
    public Transform leftFoot;
    public Transform rightFoot;
    public Transform tailVisual;
    // Sit blink / night roost. Build only; LateUpdate must not Find.
    public SpriteRenderer eyeRenderer;
    // Rest pupil on Head Visual. Walk / Peck forage, Sit preen / scratch
    // drop, Sit idleLook glance, Walk hop-turn drop, and Sit hop-turn
    // drop from here.
    public Vector3 eyeRestLocal;
    // Attack stoop nictitating cover. Build only; LateUpdate must not Find.
    public SpriteRenderer nictRenderer;
    // Dirt Walk / Peck forage / Sit preen / Sit scratch / Walk
    // hop-turn / Sit hop-turn lid. Child of the pupil so the drop
    // rides it.
    public SpriteRenderer lidRenderer;
    // Peck swallow gullet. Child of Neck Visual; LateUpdate must not Find.
    public SpriteRenderer cropRenderer;
    public Vector3 cropRestLocal;
    public Vector2 cropRestSize;
    // Alarm/Call shout / Attack stoop tongue. Child of Beak Visual;
    // LateUpdate must not Find.
    public SpriteRenderer tongueRenderer;
    public Vector3 tongueRestLocal;
    public Vector2 tongueRestSize;
    // Hen wattle on Head Visual. Chicken only; LateUpdate must not Find.
    public SpriteRenderer wattleRenderer;
    public Vector3 wattleRestLocal;
    public Vector2 wattleRestSize;
    // Hen comb on Head Visual. Chicken only; LateUpdate must not Find.
    // Wattle hang is cryWattleAmp / stoopWattleAmp / walkWattleAmp /
    // walkTurnWattleAmp / sitLookWattleAmp / sitPreenWattleAmp /
    // sitScratchWattleAmp / sitTurnWattleAmp / landSettleWattleAmp /
    // peckWattleAmp / takeoffWattleAmp / landWattleAmp /
    // alarmLookWattleAmp;
    // comb nod is cryCombAmp / alarmLookCombAmp /
    // walkCombAmp / walkTurnCombAmp / sitTurnCombAmp / sitLookCombAmp /
    // sitPreenCombAmp / sitScratchCombAmp / landSettleCombAmp /
    // peckCombAmp / takeoffCombAmp / landCombAmp.
    public SpriteRenderer combFrontRenderer;
    public Vector3 combFrontRestLocal;
    public Vector2 combFrontRestSize;
    public SpriteRenderer combBackRenderer;
    public Vector3 combBackRestLocal;
    public Vector2 combBackRestSize;
    // Picture only. Scale.x = facing; do not touch Rigidbody2D or the perch on the body.
    public Transform lookRoot;

    // Layout guide: one semi-transparent box per collider. No Crow/Chicken PNG.
    private static readonly Color BodyColor = new Color(0.1f, 0.2f, 1f, 0.5f);
    private static readonly Color NeckColor = new Color(0.5f, 0.5f, 0.5f, 0.5f);
    private static readonly Color HeadColor = new Color(1f, 1f, 0f, 0.5f);
    private static readonly Color BeakColor = new Color(1f, 0.55f, 0.1f, 0.5f);
    // Hen mark. Crow/woodpecker stay a yellow box; without this the
    // Chicken scene reads as a fat crow. Picture only — no collider.
    private static readonly Color CombColor = new Color(0.82f, 0.12f, 0.10f, 0.92f);
    private static readonly Color WattleColor = new Color(0.70f, 0.08f, 0.08f, 0.88f);
    // Side-view pupil on Head Visual. Without this idleLook is a blank box.
    private static readonly Color EyeColor = new Color(0.08f, 0.07f, 0.06f, 0.95f);
    // Pale nictitating cover over the pupil. Attack stoop only.
    private static readonly Color NictColor = new Color(0.90f, 0.88f, 0.82f, 0.90f);
    // Head-colored lid over the pupil. Walk forage squint, not the nict.
    private static readonly Color LidColor = new Color(0.55f, 0.48f, 0.08f, 0.92f);
    // Warmer gullet on Neck Visual. Peck swallow / Alarm shout /
    // Attack stoop crop — not the hen wattle.
    private static readonly Color CropColor = new Color(0.62f, 0.38f, 0.32f, 0.82f);
    // Pink tongue in the gape. Alarm/Call shout (`cryTongueAmp`),
    // Attack stoop (`stoopTongueAmp`), and Fly/Glide dirt swoop
    // (`swoopTongueAmp`) — not Peck grab, not the crop,
    // not the hen wattle (shout swing is cryWattleAmp, stoop hang
    // is stoopWattleAmp, Walk hop is walkWattleAmp, Sit look-down
    // is sitLookWattleAmp, Sit preen nibble is sitPreenWattleAmp,
    // Sit scratch kick is sitScratchWattleAmp, Sit hop-turn is
    // sitTurnWattleAmp, Walk hop-turn is walkTurnWattleAmp,
    // Sit land-ruffle is landSettleWattleAmp, Peck swallow is
    // peckWattleAmp, Fly first-air is takeoffWattleAmp, Fly/Glide
    // flare is landWattleAmp, Alarm glance is alarmLookWattleAmp),
    // not the hen
    // comb (shout nod is cryCombAmp, Walk hop nod is walkCombAmp,
    // Walk hop-turn nod is walkTurnCombAmp, Sit hop-turn nod is
    // sitTurnCombAmp, Sit look-down nod is sitLookCombAmp,
    // Sit preen nibble nod is sitPreenCombAmp, Sit scratch kick
    // nod is sitScratchCombAmp, Sit land-ruffle nod is
    // landSettleCombAmp, Peck swallow nod is peckCombAmp,
    // Fly first-air nod is takeoffCombAmp, Fly/Glide flare nod
    // is landCombAmp, Alarm glance nod is alarmLookCombAmp).
    private static readonly Color TongueColor = new Color(0.82f, 0.42f, 0.48f, 0.90f);
    private static readonly Color TailColor = new Color(0.1f, 0.75f, 0.85f, 0.5f);
    private static readonly Color HumerusNear = new Color(0f, 0.6f, 0f, 0.5f);
    private static readonly Color HumerusFar = new Color(0.4f, 0.8f, 0.2f, 0.5f);
    private static readonly Color UlnaNear = new Color(0.8f, 0.1f, 0.1f, 0.5f);
    private static readonly Color UlnaFar = new Color(1f, 0.5f, 0.5f, 0.5f);
    private static readonly Color HandNear = new Color(1f, 0.5f, 0f, 0.5f);
    private static readonly Color HandFar = new Color(1f, 0.7f, 0.3f, 0.5f);
    // B-WING-4: the feather fan. Slate, so the fan reads against the sky and
    // apart from the wing bones' own palette (green/red/orange).
    private static readonly Color FeatherNear = new Color(0.13f, 0.14f, 0.18f, 0.55f);
    private static readonly Color FeatherFar = new Color(0.45f, 0.46f, 0.52f, 0.5f);
    private static readonly Color ThighNear = new Color(0.5f, 0f, 0.5f, 0.5f);
    private static readonly Color ThighFar = new Color(0.7f, 0.4f, 0.7f, 0.5f);
    private static readonly Color ShankNear = new Color(0.5f, 0.25f, 0f, 0.5f);
    private static readonly Color ShankFar = new Color(0.7f, 0.45f, 0.2f, 0.5f);
    private static readonly Color FootNear = new Color(0.4f, 0f, 0f, 0.5f);
    private static readonly Color FootFar = new Color(0.8f, 0.3f, 0.3f, 0.5f);

    [Header("View: almost in profile")]
    public float viewAngleDegrees = 80f;
    [Tooltip("Does not enter joint anchors: otherwise it is a step forward, not a viewpoint.")]
    public float hipHalfSpacing = 0.012f;
    public float visualSideOffset = 0.028f;
    public float jointMarkerDiameter = 0.018f;

    [Header("Segment sizes, m")]
    public Vector2 bodySize = new Vector2(0.14f, 0.06f);
    public Vector2 neckSize = new Vector2(0.028f, 0.035f);
    public Vector2 headSize = new Vector2(0.042f, 0.038f);
    public Vector2 beakSize = new Vector2(0.032f, 0.012f);
    public Vector2 tailSize = new Vector2(0.09f, 0.022f);
    public Vector2 wingHumerusSize = new Vector2(0.032f, 0.055f);
    public Vector2 wingUlnaSize = new Vector2(0.040f, 0.070f);
    public Vector2 wingHandSize = new Vector2(0.070f, 0.110f);
    public Vector2 thighSize = new Vector2(0.022f, 0.040f);
    public Vector2 shankSize = new Vector2(0.018f, 0.055f);
    public Vector2 footSize = new Vector2(0.05f, 0.022f);

    // ---- B-GROUND-1 "The claws hold" (the owner's sheet "Трение когтей", 25.09) ----
    // The flock rig's feet were pure look: CreateVisualPart adds no collider, so
    // the ground never saw a claw and the Walk skated (the body glided at
    // walkSpeed while the legs cycled in the air). One narrow pad per foot, on
    // an explicit PhysicsMaterial2D (collider-materials.mdc: never the engine's
    // implicit 0.4), opened by BirdController only while that foot's plant
    // holds. All of it is off unless -birdClawGrip is passed, so every older
    // run keeps the shape it was accepted with.
    [Header("Claw grip (B-GROUND-1)")]
    [Tooltip("Claw pad friction (-birdClawFriction). 0 = a bare pad, no grip.")]
    public float clawFriction = 2.4f;
    [Tooltip("Pad width as a share of the foot box (-birdClawPad).")]
    public float clawPadWidth = 0.85f;
    [Tooltip("Share of the bird's weight carried by a planted claw (-birdClawShare).")]
    public float clawWeightShare;
    public BoxCollider2D leftClaw;
    public BoxCollider2D rightClaw;
    private PhysicsMaterial2D clawMaterial;
    public static bool ClawGrip;
    public static float ClawFrictionKnob = -1f;
    public static float ClawPadKnob = -1f;
    public static float ClawShareKnob = -1f;
    public static float ClawStrideKnob = -1f;
    public static float ClawSpeedKnob = -1f;
    public static bool ClawPin = true;
    // B-PERCH-25: the grounded settle gain (1/s, -birdGroundSettle; <0 = the code
    // default). The plate hold cancels gravity, so a bird that stopped flying used
    // to rest wherever it was inside the hold's +0.04 m window (take bPCH24c:
    // bodyY -1.8190 against the -1.8530 stance root) and every phase-8 close read
    // named the drawn feet floating over the drawn ground. The gain draws the
    // grounded body back down to the stance root; there the drawn feet meet the
    // grass (the spawn frames of the same take read 0 +- 1 px).
    public static float GroundSettleKnob = -1f;

    // ---- B-PERCH-26 "the head's own drop" (the phase-8 head/neck attack) ----
    // The peck's jab used a hard 0.08 m drop of the neck's own anchor (the whole head
    // plate rides with it). The eyes' phase-8 reads name both halves of the fault: the
    // beak never reaches the grass AND the head's box sinks into the body. The knob
    // makes the drop a per-take dose (-birdPeckNeckDrop, metres; <0 = the code default).
    public static float PeckNeckDropKnob = -1f;
    // B-PERCH-27: the body's own lowering at the jab, metres (picture squat). Per-take dose.
    public static float PeckBodyDropKnob = -1f;

    // ---- B-VIS-WALK "the visible walk" (the owner's acceptance, 25.09) ----
    // The owner read the film as "она просто наклоняется и скользит": the drawn
    // Walk swept the feet a couple of centimetres and never lifted them, so no
    // step cycle read. With -birdStepAmp the flock Walk picture becomes a real
    // stroke in the leg's own plane - a sweep from front to back through the
    // claw's plant band, then a folded, lifted return - solved to thigh and
    // shank angles by a two-link IK. Picture only, no force, no collider.
    // Off by default: every older run keeps its shape.
    public static float StepAmpKnob;
    public static float StepReachKnob = -1f;
    public static float StepLiftKnob = -1f;

    // ---- B-GROUND-2 "stand or walk" (the owner's correction, 25.09) ----
    // The owner watched the pack film and named the defect: birds SLIDING along
    // the ground ("либо стоят, либо ходят" — his rule). Off the stand the flock
    // body rides a hover with no ground contact at all, so a sitting or gliding
    // bird kept travelling (read in the pack take: 0.97-1.49 m per episode at up
    // to 0.719 m/s while mode was Sit/Glide, and the walks glided at the old
    // 0.7 m/s cap). With -birdNoSlide a grounded flock bird may only STAND or
    // WALK: the claw grip's travel clock and the drawn step switch on with it,
    // and the ground governor holds the body to the gait's own pace while Walk
    // drives it and to a stand in every other mode. Off by default: every older
    // run keeps its shape.
    public static bool NoSlide;
    public static float NoSlideBrakeKnob = -1f;

    // ---- B-TURN-3 "arc turns" (the strategy order's head, 25.09) ----
    // The brain's SetFacing moves the nose in one step, so an in-air reversal was a
    // mirror: the thrust turned with the nose while the body still carried the old
    // speed (the ruler's in-air |dvx|/0.1 s read p99 1.54, max 3.08 m/s at PRE).
    // With -birdAirArc a commanded reversal in the air becomes an arc: the nose
    // sweeps up through the top (a wingover) and the facing flips only once the
    // body's vx has crossed zero. Off by default: every older run keeps its shape.
    public static bool AirArc;
    public static float AirArcTimeKnob = -1f;
    public static float AirArcMinVxKnob = -1f;
    public static float ArcPeakKnob = -1f;

    // ---- B-TURN-3 "flock spacing" (separation + alignment in the flock context) ----
    // The flock Update writes mode and facing only; with -birdFlockSpace it also
    // writes one steering vector per airborne member (separation from close
    // neighbours, alignment with their velocity), applied by BirdFlight as an
    // acceleration. Off by default: the pack of every older run keeps its shape.
    public static bool FlockSpace;
    public static float FlockSepKnob = -1f;
    public static float FlockAliKnob = -1f;

    // ---- B-ARC-2 "the fall law's own control" ----
    // The recorder's fall law reads a real fall at its contact. To prove the law
    // catches one, the control take turns the base's landing clamp OFF: a brain's
    // ground mode mid-air is obeyed again (the B-NAT-0 free fall, 9.8 m/s²).
    // Unset (-1) keeps the base's clamp on; every older run keeps its shape.
    public static float LandingRuleKnob = -1f;

    // ---- B-ARC-2 "the law annals" ----
    // The A/B switch for the annals' before/after reads: 0 flies the pitch guard's
    // old shape (a commanded arc latches as it did), 1/off flies the refined one.
    // Inert by default; a row changes only when a take asks for the old shape.
    public static float FallLawKnob = -1f;

    // The bird lane reads its own switches here (HeadlessTrial.cs belongs to the
    // shared stand; Bird*.cs is this stream's file, the Dog.cs pattern).
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void ReadClawSwitches()
    {
        string[] args = System.Environment.GetCommandLineArgs();
        for (int i = 0; i + 1 < args.Length; i++)
        {
            if (args[i] == "-birdClawGrip")
                ClawGrip = ClawFloat(args[i + 1], 1f) > 0.5f;
            else if (args[i] == "-birdClawFriction")
                ClawFrictionKnob = ClawFloat(args[i + 1], -1f);
            else if (args[i] == "-birdClawPad")
                ClawPadKnob = ClawFloat(args[i + 1], -1f);
            else if (args[i] == "-birdClawShare")
                ClawShareKnob = ClawFloat(args[i + 1], -1f);
            else if (args[i] == "-birdClawStride")
                ClawStrideKnob = ClawFloat(args[i + 1], -1f);
            else if (args[i] == "-birdClawSpeed")
                ClawSpeedKnob = ClawFloat(args[i + 1], -1f);
            else if (args[i] == "-birdClawPin")
                ClawPin = ClawFloat(args[i + 1], 1f) > 0.5f;
            else if (args[i] == "-birdGroundSettle")
                GroundSettleKnob = ClawFloat(args[i + 1], -1f);
            else if (args[i] == "-birdPeckNeckDrop")
                PeckNeckDropKnob = ClawFloat(args[i + 1], -1f);
            else if (args[i] == "-birdPeckBodyDrop")
                PeckBodyDropKnob = ClawFloat(args[i + 1], -1f);
            else if (args[i] == "-birdStepAmp")
                StepAmpKnob = ClawFloat(args[i + 1], 0f);
            else if (args[i] == "-birdStepReach")
                StepReachKnob = ClawFloat(args[i + 1], -1f);
            else if (args[i] == "-birdStepLift")
                StepLiftKnob = ClawFloat(args[i + 1], -1f);
            else if (args[i] == "-birdNoSlide")
                NoSlide = ClawFloat(args[i + 1], 1f) > 0.5f;
            else if (args[i] == "-birdNoSlideBrake")
                NoSlideBrakeKnob = ClawFloat(args[i + 1], -1f);
            else if (args[i] == "-birdAirArc")
                AirArc = ClawFloat(args[i + 1], 1f) > 0.5f;
            else if (args[i] == "-birdArcTime")
                AirArcTimeKnob = ClawFloat(args[i + 1], -1f);
            else if (args[i] == "-birdArcMinVx")
                AirArcMinVxKnob = ClawFloat(args[i + 1], -1f);
            else if (args[i] == "-birdArcPeak")
                ArcPeakKnob = ClawFloat(args[i + 1], -1f);
            else if (args[i] == "-birdFlockSpace")
                FlockSpace = ClawFloat(args[i + 1], 1f) > 0.5f;
            else if (args[i] == "-birdFlockSep")
                FlockSepKnob = ClawFloat(args[i + 1], -1f);
            else if (args[i] == "-birdFlockAli")
                FlockAliKnob = ClawFloat(args[i + 1], -1f);
            else if (args[i] == "-birdLandingRule")
                LandingRuleKnob = ClawFloat(args[i + 1], -1f);
            else if (args[i] == "-birdFallLaw")
                FallLawKnob = ClawFloat(args[i + 1], -1f);
        }
    }

    private static float ClawFloat(string raw, float fallback)
    {
        float parsed;
        if (!float.TryParse(raw, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out parsed))
            return fallback;
        return parsed;
    }

    public PhysicsMaterial2D ClawContactMaterial()
    {
        if (clawMaterial == null)
        {
            clawMaterial = new PhysicsMaterial2D("BirdClaw");
            clawMaterial.friction = Mathf.Max(0f,
                ClawFrictionKnob >= 0f ? ClawFrictionKnob : clawFriction);
            clawMaterial.bounciness = 0f;
        }
        return clawMaterial;
    }

    public float ClawWeightShare()
    {
        if (ClawShareKnob >= 0f)
            return Mathf.Clamp(ClawShareKnob, 0f, 1f);
        return Mathf.Clamp(clawWeightShare, 0f, 1f);
    }

    // The pad rides the foot's own picture (a child of the foot bone), so the
    // claw digs exactly where the drawn foot is. Solid, on BirdLimb: the matrix
    // keeps it off the other bird fixtures and the ground sees it. Built
    // disabled — BirdController opens it while the plant holds.
    private void BuildClaw(Transform foot, bool left)
    {
        if (foot == null)
            return;
        Transform vis = foot.Find("Visual");
        float dx = vis != null ? vis.localPosition.x : 0f;
        float dy = vis != null ? vis.localPosition.y : -footSize.y * 0.5f;
        float w = footSize.x * Mathf.Clamp(
            ClawPadKnob >= 0f ? ClawPadKnob : clawPadWidth, 0.3f, 1f);
        GameObject go = new GameObject(left ? "LeftClaw" : "RightClaw");
        go.transform.SetParent(foot, false);
        BoxCollider2D col = go.AddComponent<BoxCollider2D>();
        col.size = new Vector2(w, footSize.y);
        col.offset = new Vector2(dx, dy);
        col.sharedMaterial = ClawContactMaterial();
        col.enabled = false;
        if (left)
            leftClaw = col;
        else
            rightClaw = col;
    }

    [Header("Anchors on the body")]
    public float shoulderForward = 0.02f;
    public float hipBack = 0f;
    public float ankleHeelOffset = 0.012f;

    [Header("Minimum segment inertia, kg·m²")]
    [Tooltip("A 2D limb slice gives I ~ 10⁻⁶, and a 5 ms step on that I either diverges or SPD chokes the torque.")]
    public float minLimbInertia = 0.001f;
    [Tooltip("Slightly heavier than the 2D slice so the flap gesture does not spin the breast.")]
    public float minBodyInertia = 0.015f;

    [Header("Joint friction")]
    public float neckFriction = 0.002f;
    public float beakFriction = 0.0012f;
    public float tailFriction = 0.012f;
    public float shoulderFriction = 0.0008f;
    public float elbowFriction = 0.0005f;
    public float wristFriction = 0.0003f;
    public float hipFriction = 0.02f;
    public float kneeFriction = 0.015f;
    public float ankleFriction = 0.008f;

    [Header("Maximum muscle torques, N·m")]
    public float neckMuscleTorque = 0.12f;
    public float beakMuscleTorque = 0.06f;
    public float tailMuscleTorque = 0.18f;
    public float shoulderMuscleTorque = 0.28f;
    public float elbowMuscleTorque = 0.20f;
    public float wristMuscleTorque = 0.08f;
    public float hipMuscleTorque = 0.50f;
    public float kneeMuscleTorque = 0.40f;
    public float ankleExtensorTorque = 0.22f;
    public float ankleFlexorTorque = 0.10f;

    private const float BODY_MASS = 0.622f;
    private const float NECK_MASS = 0.025f;
    private const float HEAD_MASS = 0.068f;
    private const float BEAK_MASS = 0.012f;
    private const float TAIL_MASS = 0.025f;
    private const float HUMERUS_MASS = 0.016f;
    private const float ULNA_MASS = 0.013f;
    private const float HAND_MASS = 0.020f;
    private const float THIGH_MASS = 0.035f;
    private const float SHANK_MASS = 0.028f;
    private const float FOOT_MASS = 0.012f;

    private const int LEFT_WING_HUMERUS = -6;
    private const int LEFT_WING_ULNA = -5;
    private const int LEFT_WING_HAND = -4;
    private const int LEFT_LEG_THIGH = -3;
    private const int LEFT_LEG_SHANK = -2;
    private const int LEFT_LEG_FOOT = -1;
    private const int BODY_ORDER = 0;
    private const int TAIL_ORDER = 0;
    private const int NECK_ORDER = 1;
    private const int HEAD_ORDER = 2;
    private const int BEAK_ORDER = 3;
    private const int RIGHT_LEG_THIGH = 4;
    private const int RIGHT_LEG_SHANK = 5;
    private const int RIGHT_LEG_FOOT = 6;
    private const int RIGHT_WING_HUMERUS = 7;
    private const int RIGHT_WING_ULNA = 8;
    private const int RIGHT_WING_HAND = 9;

    // THE PROGRAM / B-BEND-1 (25.09): the bird's visual band must read ABOVE the
    // parent tree's crown. The plant's band: wood = branch depth (0..3), leaf pads
    // and lobes = depth+2 (up to ~5), fruit/bloom = +3 (up to ~6); the ground and
    // decor sit at -40..6. The bird's relative band (-6..+9) fell inside the plant's,
    // so the crown's leaves painted OVER the perched bird — the eyes' phase-6/7
    // fault of 25.09 ("the body/legs sink into the crown, the seated bird is partly
    // camouflaged") on every cut. One lift, both rigs, every part (the small marks
    // are HEAD_ORDER+N etc., so they ride along): band -> 10..25, still under the
    // art-missing markers (90) and the body labels (100).
    private const int VisualOrderLift = 16;

    // Feet on the groundY surface: root height = the leg chain + half the body.
    public static float StandingRootY(float groundY = -2f)
    {
        return groundY + 0.022f + 0.055f + 0.040f + 0.060f * 0.5f;
    }

    public float StandingRootOffset(float groundY = -2f)
    {
        return groundY + footSize.y + shankSize.y + thighSize.y + bodySize.y * 0.5f;
    }

    // Sensors and flight: instance sizes after ApplyKind, else crow canon.
    public static float StanceRootY(Bird owner, float groundY = -2f)
    {
        return owner != null ? owner.StandingRootOffset(groundY) : StandingRootY(groundY);
    }

    // Spawn before the instance exists. Keep in sync with species layout sizes.
    public static float KindStandingRootY(BirdKind recipe, float groundY = -2f)
    {
        if (recipe == BirdKind.Chicken)
            return groundY + 0.024f + 0.060f + 0.050f + 0.10f * 0.5f;
        return StandingRootY(groundY);
    }

    public static BirdKind ParseKind(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return BirdKind.Crow;
        switch (raw.Trim().ToLowerInvariant())
        {
            case "chicken":
            case "hen":
                return BirdKind.Chicken;
            case "woodpecker":
            case "pecker":
                return BirdKind.Woodpecker;
            default:
                return BirdKind.Crow;
        }
    }

    // Prefer the derived component when the caller can choose the type.
    // GameProcess still AddComponent<Bird>() + ApplyKind; that path stays.
    public static Bird Attach(GameObject host, BirdKind recipe, BirdRig build = BirdRig.Flock)
    {
        if (host == null)
            return null;
        Bird bird;
        switch (recipe)
        {
            case BirdKind.Chicken:
                bird = host.AddComponent<Chicken>();
                break;
            case BirdKind.Woodpecker:
                bird = host.AddComponent<Woodpecker>();
                break;
            default:
                bird = host.AddComponent<Crow>();
                break;
        }

        bird.rig = build;
        bird.ApplyKind(recipe);
        return bird;
    }

    public virtual bool WantsTreeSeat => kind != BirdKind.Chicken;
    public virtual bool ScalesFlapWithAirspeed => kind == BirdKind.Crow;
    public virtual bool UsesDirtLandingFlap => kind == BirdKind.Crow;
    public virtual BirdMode FleeBodyMode => kind == BirdKind.Chicken ? BirdMode.Walk : BirdMode.Fly;
    public virtual BirdForageStyle ForageStyle
    {
        get
        {
            if (kind == BirdKind.Chicken) return BirdForageStyle.ChickenGround;
            if (kind == BirdKind.Woodpecker) return BirdForageStyle.WoodpeckerDrum;
            return BirdForageStyle.CrowCruise;
        }
    }
    public virtual float HitHealth => kind == BirdKind.Chicken ? 8f : 10f;
    public virtual float PeckDamage => kind == BirdKind.Chicken ? 0.35f : (kind == BirdKind.Woodpecker ? 0.7f : 0.5f);

    // Crow numbers are the flying canon. Derived classes override the writes.
    public void ApplyKind(BirdKind recipe)
    {
        kind = recipe;
        ApplySpeciesLayout();
    }

    protected virtual void ApplySpeciesLayout()
    {
        switch (kind)
        {
            case BirdKind.Chicken:
                Chicken.WriteLayout(this);
                break;
            case BirdKind.Woodpecker:
                Woodpecker.WriteLayout(this);
                break;
            default:
                Crow.WriteLayout(this);
                break;
        }
    }

    private void ApplyFlightKind()
    {
        if (flight == null) return;
        ApplySpeciesFlight();
    }

    protected virtual void ApplySpeciesFlight()
    {
        switch (kind)
        {
            case BirdKind.Chicken:
                Chicken.WriteFlight(flight);
                break;
            case BirdKind.Woodpecker:
                Woodpecker.WriteFlight(flight);
                break;
            default:
                Crow.WriteFlight(flight);
                break;
        }
    }

    private void ApplyControlKind()
    {
        if (controller == null) return;
        ApplySpeciesControl();
    }

    protected virtual void ApplySpeciesControl()
    {
        switch (kind)
        {
            case BirdKind.Chicken:
                Chicken.WriteControl(controller);
                break;
            case BirdKind.Woodpecker:
                Woodpecker.WriteControl(controller);
                break;
            default:
                Crow.WriteControl(controller);
                break;
        }
    }

    public void BuildBird()
    {
        ApplyKind(kind);
        foreach (Transform child in transform)
            Destroy(child.gameObject);

        if (rig == BirdRig.Flock)
        {
            BuildFlock();
            return;
        }

        HumanSegment body = CreateSegment("Body", bodySize, BODY_MASS, BodyColor, BODY_ORDER);
        body.transform.localPosition = Vector3.zero;
        bodyCollider = body.collider;
        AddBodyAero(body);
        AddBirdFlight(body);
        ApplyFlightKind();

        HumanSegment neck = CreateSegment("Neck", neckSize, NECK_MASS, NeckColor, NECK_ORDER);
        float bodyTop = bodySize.y * 0.5f;
        neck.transform.localPosition = new Vector3(bodySize.x * 0.22f, bodyTop + neckSize.y * 0.5f, 0f);
        neck.ConnectTo(body,
            new Vector2(0f, -neckSize.y * 0.5f),
            new Vector2(bodySize.x * 0.22f, bodyTop),
            -40f, 50f);
        AddFriction(neck, neckFriction, 0.4f);
        AddMuscles(neck, neckMuscleTorque);
        neckVisual = neck.transform;

        HumanSegment head = CreateSegment("Head", headSize, HEAD_MASS, HeadColor, HEAD_ORDER);
        head.transform.localPosition = new Vector3(
            neck.transform.localPosition.x,
            neck.transform.localPosition.y + neckSize.y * 0.5f + headSize.y * 0.5f,
            0f);
        head.ConnectTo(neck,
            new Vector2(0f, -headSize.y * 0.5f),
            new Vector2(0f, neckSize.y * 0.5f),
            -35f, 35f);
        AddFriction(head, neckFriction, 0.4f);
        AddMuscles(head, neckMuscleTorque);
        headSegment = head;
        headCollider = head.collider;
        AttachRagdollBeak(head);
        AttachHenMarks(head.transform, head.visual);
        AttachEye(head.transform, head.visual);
        AttachCrop(neck.transform, neck.visual);
        AttachTongue(beakSegment != null ? beakSegment.transform : null,
            beakSegment != null ? beakSegment.visual : null);

        HumanSegment tail = CreateSegment("Tail", tailSize, TAIL_MASS, TailColor, TAIL_ORDER);
        float bodyBack = -bodySize.x * 0.5f;
        tail.transform.localPosition = new Vector3(bodyBack - tailSize.x * 0.5f, 0f, 0f);
        tail.ConnectTo(body,
            new Vector2(tailSize.x * 0.5f, 0f),
            new Vector2(bodyBack, 0f),
            -50f, 50f);
        AddFriction(tail, tailFriction, 0.5f);
        AddMuscles(tail, tailMuscleTorque);

        CreateWing("RightWing", 1f, body,
            RIGHT_WING_HUMERUS, RIGHT_WING_ULNA, RIGHT_WING_HAND,
            HumerusNear, UlnaNear, HandNear);
        CreateWing("LeftWing", -1f, body,
            LEFT_WING_HUMERUS, LEFT_WING_ULNA, LEFT_WING_HAND,
            HumerusFar, UlnaFar, HandFar);

        CreateLeg("RightLeg", 1f, body,
            RIGHT_LEG_THIGH, RIGHT_LEG_SHANK, RIGHT_LEG_FOOT,
            ThighNear, ShankNear, FootNear);
        CreateLeg("LeftLeg", -1f, body,
            LEFT_LEG_THIGH, LEFT_LEG_SHANK, LEFT_LEG_FOOT,
            ThighFar, ShankFar, FootFar);

        DisableCollisionsBetweenSegments();
        StampContactLayers();

        sensors = gameObject.AddComponent<BirdSensors>();
        sensors.Initialize();
        controller = gameObject.AddComponent<BirdController>();
        BindController(controller);
        ApplyControlKind();
        AttachHit();

        ActuatorDriver actuators = gameObject.AddComponent<ActuatorDriver>();
        actuators.RebuildCache();
    }

    public void AddNumberLabel(int number)
    {
        Transform labelParent = null;
        if (headVisual != null)
        {
            Transform vis = headVisual.Find("Visual");
            labelParent = vis != null ? vis : headVisual;
        }
        else if (headSegment != null)
            labelParent = headSegment.visual != null ? headSegment.visual : headSegment.transform;
        if (labelParent == null)
        {
            Transform body = transform.Find("Body");
            labelParent = body != null ? body : transform;
        }
        GameObject labelObj = new GameObject("NumberLabel");
        labelObj.transform.SetParent(labelParent, false);
        float headHalf = headVisual != null ? headSize.y * 0.5f
            : (headSegment != null ? headSegment.size.y * 0.5f : 0.04f);
        labelObj.transform.localPosition = new Vector3(0f, headHalf + 0.08f, -0.1f);
        TextMesh textMesh = labelObj.AddComponent<TextMesh>();
        textMesh.text = "B" + number;
        textMesh.fontSize = 28;
        textMesh.characterSize = 0.04f;
        textMesh.color = Color.black;
        textMesh.anchor = TextAnchor.MiddleCenter;
        Renderer renderer = labelObj.GetComponent<Renderer>();
        renderer.sortingOrder = 100;
    }

    // One body: mass and collider on the torso; the rest are children without physics.
    private void BuildFlock()
    {
        HumanSegment body = CreateSegment("Body", bodySize, 1f, BodyColor, BODY_ORDER);
        body.transform.localPosition = Vector3.zero;
        bodyCollider = body.collider;
        if (body.rb != null)
        {
            // Headless compares CSV: no interpolation. In Play there is one body;
            // Interpolate is cheaper than 16 bodies, and the 60 Hz frame does not jitter.
            body.rb.interpolation = HeadlessTrial.Active
                ? RigidbodyInterpolation2D.None
                : RigidbodyInterpolation2D.Interpolate;
            body.rb.collisionDetectionMode = CollisionDetectionMode2D.Discrete;
            body.rb.sleepMode = RigidbodySleepMode2D.StartAwake;
        }

        // Perch is a trigger volume on BirdLimb: it finds BirdPerch pads and
        // dirt, it does not generate solver contacts. Wings and legs are look.
        float legH = thighSize.y + shankSize.y + footSize.y;
        GameObject perchGo = new GameObject("Perch");
        perchGo.transform.SetParent(body.transform, false);
        perchGo.transform.localPosition = Vector3.zero;
        perchCollider = perchGo.AddComponent<BoxCollider2D>();
        perchCollider.size = new Vector2(Mathf.Max(footSize.x, bodySize.x * 0.55f), legH);
        perchCollider.offset = new Vector2(0f, -bodySize.y * 0.5f - legH * 0.5f);
        perchCollider.isTrigger = true;

        AddBodyAero(body);
        AddBirdFlight(body);
        ApplyFlightKind();

        // Turn is a Look mirror. The collider body always stays at scale 1:
        // otherwise the perch slides and PhysX 2D breaks the normals.
        GameObject lookObject = new GameObject("Look");
        lookRoot = lookObject.transform;
        lookRoot.SetParent(body.transform, false);
        lookRoot.localPosition = Vector3.zero;
        lookRoot.localScale = Vector3.one;

        float bodyTop = bodySize.y * 0.5f;
        Transform neck = CreateVisualPart("Neck", neckSize, NeckColor, NECK_ORDER, lookRoot);
        neck.localPosition = new Vector3(bodySize.x * 0.22f, bodyTop + neckSize.y * 0.5f, 0f);

        Transform head = CreateVisualPart("Head", headSize, HeadColor, HEAD_ORDER, neck);
        head.localPosition = new Vector3(0f, neckSize.y * 0.5f + headSize.y * 0.5f, 0f);
        neckVisual = neck;
        headVisual = head;
        headSegment = null;
        BoxCollider2D headCol = head.gameObject.AddComponent<BoxCollider2D>();
        headCol.size = headSize;
        headCollider = headCol;
        AttachFlockBeak(head);
        AttachHenMarks(head);
        AttachEye(head);
        AttachCrop(neck);
        AttachTongue(beakVisual);

        Transform tail = CreateVisualPart("Tail", tailSize, TailColor, TAIL_ORDER, lookRoot);
        tail.localPosition = new Vector3(-bodySize.x * 0.5f - tailSize.x * 0.5f, 0f, 0f);
        tailVisual = tail;

        rightWingRoot = CreateVisualWing(1f, lookRoot);
        leftWingRoot = CreateVisualWing(-1f, lookRoot);
        rightUlna = rightWingRoot != null ? rightWingRoot.Find("RightWingUlna") : null;
        leftUlna = leftWingRoot != null ? leftWingRoot.Find("LeftWingUlna") : null;
        rightHand = rightUlna != null ? rightUlna.Find("RightWingHand") : null;
        leftHand = leftUlna != null ? leftUlna.Find("LeftWingHand") : null;

        rightThigh = CreateVisualLeg(1f, lookRoot);
        leftThigh = CreateVisualLeg(-1f, lookRoot);
        // B-GROUND-1: the claws. Pads on the feet's own picture, opened on the plant.
        BuildClaw(leftFoot, true);
        BuildClaw(rightFoot, false);

        StampContactLayers();

        sensors = gameObject.AddComponent<BirdSensors>();
        sensors.Initialize();
        controller = gameObject.AddComponent<BirdController>();
        controller.flockRig = true;
        ApplyControlKind();
        ApplyLookFacing(controller.FacingSign());
        AttachHit();
    }

    // The picture faces the flight direction. Body physics is not scaled.
    public void ApplyLookFacing(float sign)
    {
        if (lookRoot == null) return;
        lookRoot.localScale = new Vector3(sign >= 0f ? 1f : -1f, 1f, 1f);
    }

    // Flock: a sprite as on a segment, without HumanSegment on every feather.
    private Transform CreateVisualPart(
        string name, Vector2 size, Color color, int order, Transform parent)
    {
        GameObject go = new GameObject(name);
        HumanSegment bake = go.AddComponent<HumanSegment>();
        bake.InitializeVisualOnly(name, size, color, parent, order + VisualOrderLift, HumanVisualShape.Box);
        Object.DestroyImmediate(bake);
        return go.transform;
    }

    private static void OffsetVisual(Transform bone, float x, float y)
    {
        if (bone == null) return;
        Transform visual = bone.Find("Visual");
        if (visual != null)
            visual.localPosition = new Vector3(x, y, 0f);
    }

    private Transform CreateVisualWing(float direction, Transform body)
    {
        float visualShift = direction * visualSideOffset;
        float shoulderY = bodySize.y * 0.30f;
        const float foldDeg = -90f;
        string side = direction > 0f ? "RightWing" : "LeftWing";
        bool near = direction > 0f;

        Transform humerus = CreateVisualPart(side + "Humerus", wingHumerusSize, near ? HumerusNear : HumerusFar, direction > 0f ? RIGHT_WING_HUMERUS : LEFT_WING_HUMERUS, body);
        humerus.localPosition = new Vector3(shoulderForward, shoulderY, 0f);
        humerus.localRotation = Quaternion.Euler(0f, 0f, foldDeg);
        OffsetVisual(humerus, visualShift, -wingHumerusSize.y * 0.5f);

        Transform ulna = CreateVisualPart(side + "Ulna", wingUlnaSize, near ? UlnaNear : UlnaFar, direction > 0f ? RIGHT_WING_ULNA : LEFT_WING_ULNA, humerus);
        ulna.localPosition = new Vector3(0f, -wingHumerusSize.y, 0f);
        OffsetVisual(ulna, visualShift, -wingUlnaSize.y * 0.5f);

        Transform hand = CreateVisualPart(side + "Hand", wingHandSize, near ? HandNear : HandFar, direction > 0f ? RIGHT_WING_HAND : LEFT_WING_HAND, ulna);
        hand.localPosition = new Vector3(0f, -wingUlnaSize.y, 0f);
        OffsetVisual(hand, visualShift, -wingHandSize.y * 0.5f);

        // B-WING-4: the wing's area. The bones are the leading edge; the fan is
        // the chord — primaries on the hand, secondaries on the ulna. Folded it
        // lies inside the bone's own length (the B-WING-3 rail); open it fans
        // toward the bone's trailing side (local +X — at the glide's fold −120
        // that maps to down-back).
        WingFeatherFan fan = BuildWingFan(side, near, visualShift, hand, ulna);
        if (direction > 0f)
            rightFan = fan;
        else
            leftFan = fan;

        return humerus;
    }

    // B-WING-4: the feather fan's numbers. Angle 0 = the feather lies on the
    // bone (its own −Y axis); positive turns it toward the bone's local +X —
    // the trailing side. FOLDED: every feather ~6 deg off the bone at 45 % of
    // its length, so the folded wing keeps the B-WING-3 rail (the fan stays
    // inside the bone's own box).
    // B-WING-4 a3: one continuous fan, not a comb of slats. The angle sweeps
    // monotonically from 134 deg at the armpit to 48 deg at the tip, so each
    // feather overlaps its neighbour and the trailing edge is a solid chord
    // with staggered tips — the area the eye asked for; the outermost sweeps
    // back-out past the hand as the wing tip.
    // B-WING-5: the fan's contour. The B-WING-4 plate was a rigid rectangle
    // (the eyes' own #1 fault: "жёсткие прямоугольники без сужения"); the
    // plate is a feather — wide at the root (the fold side, where the lateral
    // overlap widens 3.0 -> 5.8 cm) drawn to a point at the tip. p2 made the
    // blade fuller (taper 2.6) but kept the reads' saw: "выраженная зубчатая
    // пила" — the tips, 2.5 cm apart on the envelope, cut 1.6-2 cm notches
    // between themselves. p3 (this shape): the blade is full-bodied through
    // the middle and needles only in the last eighth (exponent 6), so each
    // blade covers its neighbour's tip and the fan reads as ONE contoured
    // surface with the tips staggered into a fine serration at the trailing
    // edge; the folded pose additionally narrows in X (0.62) so the pack
    // stays inside the bone's own box — the B-WING-3 rail. Geometry, angles,
    // lengths, order and the drive are untouched; the sprite is picture only.
    // B-WING-6: the folded pack lies ALONG THE BACK. The blades' own fold
    // target turns from the tuck (172, tips to the bone root) to 8 deg on the
    // bone's trailing side — laid along the bone toward its tip — so with the
    // ground fold's chain (humerus -90, ulna 165, hand -170; see
    // BirdController.GroundFoldUlnaZ) the whole fan lies along the back and
    // the tips step toward the tail. The folded plates read LONG (scale 0.85,
    // the wingtip reaching the tail's tip) and NARROW (width 0.45), so the
    // pack is a graded sheaf, not a knot of capsules. Spread targets and
    // drive untouched.
    private const float FeatherFoldDeg = 8f;
    // B-WING-6 p3: the SECONDARY plates fold the other way — tucked back along
    // the forearm toward the elbow. Laid along the ulna's own arrow they poked
    // forward past the chest (the eyes' read of p2 named that as the top fault);
    // tucked they cover the forearm and stay inside the pack.
    private const float FeatherFoldDegSec = 172f;
    private const float FeatherFoldScale = 0.85f;
    private const float FeatherFoldWidthScale = 0.45f;
    private const float FeatherRootWidth = 0.058f;
    // B-WING-7 p3: the covert layer's own geometry. Through p2 the coverts were
    // clones of the primaries' width on shorter blades - square plates (58 x
    // 58-76 mm) whose tips reached the primaries' rim, and the sheet reads named
    // them the second row of blunt rectangular teeth ("тупые прямоугольники
    // одной ширины"; "два слоя зубьев"). The spread is now a true covert layer:
    // a narrower plate (40 mm) on a shorter blade, tucked inside the primaries'
    // rim. The sprite's width is picture-only (the box keeps the fleet width);
    // the covert blade's own length is this layer's geometry - the rows are
    // compared in the report. The fold tuck (FeatherFoldDegSec) and the drive
    // are not touched.
    private const float FeatherSecWidth = 0.040f;
    // B-WING-7: the taper through the blade's body. The p3 blade held its root
    // width through eight tenths and needled only in the last eighth — the
    // eyes' p3 spread reads still named the blunt plate ("тупые
    // прямоугольники и вытянутые капсулы"); the first pass (a 45 % body at a
    // 7-power point) still read "тупые одинаковые прямоугольники... без
    // естественного сужения" at the sheet's own scale. p2 spends 72 % of the
    // root width along the body and draws the point over the last quarter, so
    // each plate's visible end is a long sliver (a primary's own tip) while
    // the overlap stays dense — the sim at the sheet scale reads both the
    // wedge tips and the single sail (the 1.6-2 cm "пила" of the p2-ERA
    // near-linear taper does not return: the width leaves the neighbours' tip
    // spacing only in the last tenth). Sprite only — the collider, the fan
    // numbers and the drive take no part.
    private const float FeatherBodyTaper = 0.65f;
    private const float FeatherBodyPow = 1.4f;
    private const float FeatherTipPow = 2.0f;
    // B-WING-8: the graded rim and the broken step. Through B-WING-7 the
    // primaries' tip reach (the perpendicular depth of a tip from the bone
    // axis) ran 80 -> 95 -> 83 mm: the rim bulged mid-fan and pinched at the
    // outer end, so the spread read "same length" whatever the blades taper.
    // p1 graded the ladder (73 -> 108 mm, monotone, +48 %); p2 broke the
    // mechanical step - the attach ladder and the angle steps varied (the
    // depths re-solved to keep the p1 grade, 71 -> 110 mm, the outermost
    // plate a clear long tip) - so the rim reads as a graded row of pointed
    // feathers with the crossings off the old regular spacing. The covert
    // layer's ladder is broken the same way (attach 15/22/17 mm, angles
    // 133/125/113/104). Lengths and angles are the drawn and the box
    // geometry - picture only; the fold tuck, the drive, the collider and
    // the physics take no part.
    // B-WING-10: DENSITY + THE VISIBLE POINT — the primaries resampled 8 -> 16 plates
    // (piecewise-linear in the index space; the covets stay 4) and the tip point
    // drawn over the last third (tip pow 4 -> 2) so it survives the take's 245 px/m.
    // Rim metric 21.5/36.0 -> 12.1/19.4 mm; the sheet-scale sim: pointed feathers on
    // a continuous surface (bw10_sim2.py). Picture only.
    private static readonly float[] FeatherPriAttach = { 0.0060f, 0.0114f, 0.0167f, 0.0231f, 0.0296f, 0.0345f, 0.0387f, 0.0444f, 0.0511f, 0.0567f, 0.0607f, 0.0652f, 0.0713f, 0.0773f, 0.0826f, 0.0880f };
    private static readonly float[] FeatherPriLen = { 0.0714f, 0.0740f, 0.0766f, 0.0790f, 0.0814f, 0.0844f, 0.0877f, 0.0911f, 0.0947f, 0.0982f, 0.1018f, 0.1057f, 0.1104f, 0.1155f, 0.1226f, 0.1297f };
    private static readonly float[] FeatherPriAngle = { 96f, 94.1f, 92.3f, 90f, 87.7f, 85f, 82.2f, 79.1f, 75.9f, 73.2f, 71.3f, 69.1f, 65.8f, 62.7f, 60.3f, 58f };
    private static readonly float[] FeatherSecAttach = { 0.008f, 0.023f, 0.045f, 0.062f };
    private static readonly float[] FeatherSecLen = { 0.044f, 0.048f, 0.052f, 0.056f };
    private static readonly float[] FeatherSecAngle = { 133f, 125f, 113f, 104f };

    private WingFeatherFan BuildWingFan(string side, bool near, float visualShift,
                                        Transform hand, Transform ulna)
    {
        if (hand == null || ulna == null)
            return null;
        Color color = near ? FeatherNear : FeatherFar;
        int n = FeatherPriAttach.Length + FeatherSecAttach.Length;
        Transform[] feathers = new Transform[n];
        float[] lengths = new float[n];
        float[] openDeg = new float[n];
        Transform[] roots = new Transform[n];
        float[] rootLens = new float[n];
        for (int i = 0; i < n; i++)
        {
            bool primary = i < FeatherPriAttach.Length;
            int k = primary ? i : i - FeatherPriAttach.Length;
            float len = primary ? FeatherPriLen[k] : FeatherSecLen[k];
            int order = primary
                ? (near ? RIGHT_WING_HAND : LEFT_WING_HAND)
                : (near ? RIGHT_WING_ULNA : LEFT_WING_ULNA);
            Transform f = CreateVisualPart(
                side + (primary ? "Primary" : "Secondary") + k,
                new Vector2(FeatherRootWidth, len), color, order - 1, primary ? hand : ulna);
            DressFeatherWedge(f, primary ? FeatherRootWidth : FeatherSecWidth, len);
            f.localPosition = new Vector3(0f, -(primary ? FeatherPriAttach[k] : FeatherSecAttach[k]), 0f);
            f.localRotation = Quaternion.Euler(0f, 0f, primary ? FeatherFoldDeg : FeatherFoldDegSec);
            f.localScale = new Vector3(FeatherFoldWidthScale, FeatherFoldScale, 1f);
            OffsetVisual(f, visualShift, -len * 0.5f);
            feathers[i] = f;
            lengths[i] = len;
            openDeg[i] = primary ? FeatherPriAngle[k] : FeatherSecAngle[k];
            roots[i] = primary ? hand : ulna;
            rootLens[i] = primary ? wingHandSize.y : wingUlnaSize.y;
        }
        return new WingFeatherFan(feathers, lengths, openDeg, roots, rootLens, FeatherPriAttach.Length);
    }

    // B-WING-5: the feather's own silhouette. Physics-only runs keep the
    // HumanSegment box sprite (its own rule); a rendering run gets the wedge —
    // full width at the root edge (the texture top, the attach side), drawn to
    // a point at the tip. The sprite measures the plate exactly (ppu from the
    // width), so the collider box, the fan geometry and every row are
    // untouched — picture only.
    private static void DressFeatherWedge(Transform f, float width, float len)
    {
        if (f == null)
            return;
        if (HeadlessTrial.Active && !HeadlessTrial.VisualCapture)
            return;
        Transform vis = f.Find("Visual");
        if (vis == null)
            return;
        SpriteRenderer sr = vis.GetComponent<SpriteRenderer>();
        if (sr == null)
            return;
        sr.sprite = FeatherSprite(width, len);
    }

    // White wedge: world size (width x len) at 1024 texels/m, so the taper
    // stays clean at the take's ~245 px/m. The root row is the full width;
    // the B-WING-7 body spends that width along the length and the last rows
    // needle to the point. The tint rides sprite.color — the parts' own path.
    private static readonly Dictionary<string, Sprite> FeatherSprites = new Dictionary<string, Sprite>();

    private static Sprite FeatherSprite(float width, float len)
    {
        const float pixelsPerMeter = 1024f;
        int w = Mathf.Clamp(Mathf.RoundToInt(width * pixelsPerMeter), 8, 512);
        int h = Mathf.Clamp(Mathf.RoundToInt(len * pixelsPerMeter), 16, 2048);
        string key = w + "x" + h;
        Sprite cached;
        if (FeatherSprites.TryGetValue(key, out cached) && cached != null)
            return cached;
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;
        tex.wrapMode = TextureWrapMode.Clamp;
        Color[] px = new Color[w * h];
        float half = w * 0.5f;
        for (int y = 0; y < h; y++)
        {
            float s = 1f - (float)y / (h - 1);            // 0 at the root edge, 1 at the tip
            float taper = (1f - FeatherBodyTaper * Mathf.Pow(s, FeatherBodyPow))  // the body
                        * (1f - Mathf.Pow(s, FeatherTipPow));                      // the point
            float halfNow = half * taper;
            for (int x = 0; x < w; x++)
            {
                float d = Mathf.Abs(x + 0.5f - half);
                px[y * w + x] = d <= halfNow ? Color.white : Color.clear;
            }
        }
        tex.SetPixels(px);
        tex.Apply(false, false);
        Sprite created = Sprite.Create(tex, new Rect(0, 0, w, h),
            new Vector2(0.5f, 0.5f), w / width);
        FeatherSprites[key] = created;
        return created;
    }

    private Transform CreateVisualLeg(float direction, Transform body)
    {
        float visualShift = direction * visualSideOffset;
        float hipY = -bodySize.y * 0.5f;
        string side = direction > 0f ? "RightLeg" : "LeftLeg";
        bool near = direction > 0f;

        Transform thigh = CreateVisualPart(side + "Thigh", thighSize, near ? ThighNear : ThighFar, direction > 0f ? RIGHT_LEG_THIGH : LEFT_LEG_THIGH, body);
        thigh.localPosition = new Vector3(-hipBack, hipY, 0f);
        OffsetVisual(thigh, visualShift, -thighSize.y * 0.5f);

        Transform shank = CreateVisualPart(side + "Shank", shankSize, near ? ShankNear : ShankFar, direction > 0f ? RIGHT_LEG_SHANK : LEFT_LEG_SHANK, thigh);
        shank.localPosition = new Vector3(0f, -thighSize.y, 0f);
        OffsetVisual(shank, visualShift, -shankSize.y * 0.5f);

        Transform foot = CreateVisualPart(side + "Foot", footSize, near ? FootNear : FootFar, direction > 0f ? RIGHT_LEG_FOOT : LEFT_LEG_FOOT, shank);
        foot.localPosition = new Vector3(-ankleHeelOffset + footSize.x * 0.5f, -shankSize.y, 0f);
        OffsetVisual(foot, visualShift, -footSize.y * 0.5f);

        if (direction > 0f)
        {
            rightShank = shank;
            rightFoot = foot;
        }
        else
        {
            leftShank = shank;
            leftFoot = foot;
        }
        return thigh;
    }

    private void CreateWing(string sideName, float direction, HumanSegment body,
                            int humerusOrder, int ulnaOrder, int handOrder,
                            Color humerusColor, Color ulnaColor, Color handColor)
    {
        float visualShift = direction * visualSideOffset;
        float shoulderY = bodySize.y * 0.30f;
        Vector2 shoulderRoot = (Vector2)body.transform.localPosition
            + new Vector2(shoulderForward, shoulderY);

        // Folded along the back: the bone points toward −X. jointAngle ≈ 90° when
        // the body is at 0; see the convention jointAngle = parent − self.
        const float foldDeg = -90f;
        Quaternion fold = Quaternion.Euler(0f, 0f, foldDeg);
        Vector2 tipDir = new Vector2(-1f, 0f);

        float humerusLen = wingHumerusSize.y;
        float ulnaLen = wingUlnaSize.y;
        float handLen = wingHandSize.y;

        HumanSegment humerus = CreateSegment(sideName + "Humerus", wingHumerusSize, HUMERUS_MASS, humerusColor, humerusOrder, HumanVisualShape.Box, true);
        humerus.transform.localPosition = shoulderRoot + tipDir * (humerusLen * 0.5f);
        humerus.transform.localRotation = fold;
        humerus.SetVisualOffset(visualShift);
        humerus.ConnectTo(body,
            new Vector2(0f, humerusLen * 0.5f),
            new Vector2(shoulderForward, shoulderY),
            25f, 165f);
        AddFriction(humerus, shoulderFriction, 0.5f);
        AddMuscles(humerus, shoulderMuscleTorque);

        HumanSegment ulna = CreateSegment(sideName + "Ulna", wingUlnaSize, ULNA_MASS, ulnaColor, ulnaOrder, HumanVisualShape.Box, true);
        ulna.transform.localPosition = shoulderRoot + tipDir * (humerusLen + ulnaLen * 0.5f);
        ulna.transform.localRotation = fold;
        ulna.SetVisualOffset(visualShift);
        ulna.ConnectTo(humerus,
            new Vector2(0f, ulnaLen * 0.5f),
            new Vector2(0f, -humerusLen * 0.5f),
            -100f, 5f);
        AddFriction(ulna, elbowFriction, 0.4f);
        AddMuscles(ulna, elbowMuscleTorque);

        HumanSegment hand = CreateSegment(sideName + "Hand", wingHandSize, HAND_MASS, handColor, handOrder, HumanVisualShape.Box, true);
        hand.transform.localPosition = shoulderRoot + tipDir * (humerusLen + ulnaLen + handLen * 0.5f);
        hand.transform.localRotation = fold;
        hand.SetVisualOffset(visualShift);
        hand.ConnectTo(ulna,
            new Vector2(0f, handLen * 0.5f),
            new Vector2(0f, -ulnaLen * 0.5f),
            -55f, 55f);
        AddFriction(hand, wristFriction, 0.3f);
        AddMuscles(hand, wristMuscleTorque);
    }

    private void CreateLeg(string sideName, float direction, HumanSegment body,
                           int thighOrder, int shankOrder, int footOrder,
                           Color thighColor, Color shankColor, Color footColor)
    {
        float visualShift = direction * visualSideOffset;
        float hipY = -bodySize.y * 0.5f;
        // Leg spread is only on Visual: the X axis is forward, not left-right.
        Vector2 hipRoot = (Vector2)body.transform.localPosition + new Vector2(-hipBack, hipY);

        HumanSegment thigh = CreateSegment(sideName + "Thigh", thighSize, THIGH_MASS, thighColor, thighOrder, HumanVisualShape.Box, true);
        thigh.transform.localPosition = new Vector3(hipRoot.x, hipRoot.y - thighSize.y * 0.5f, 0f);
        thigh.SetVisualOffset(visualShift);
        thigh.ConnectTo(body,
            new Vector2(0f, thighSize.y * 0.5f),
            new Vector2(-hipBack, hipY),
            -80f, 80f);
        AddFriction(thigh, hipFriction, 0.8f);
        AddMuscles(thigh, hipMuscleTorque);

        HumanSegment shank = CreateSegment(sideName + "Shank", shankSize, SHANK_MASS, shankColor, shankOrder, HumanVisualShape.Box, true);
        shank.transform.localPosition = new Vector3(hipRoot.x, hipRoot.y - thighSize.y - shankSize.y * 0.5f, 0f);
        shank.SetVisualOffset(visualShift);
        // Plus takes the lower shank backward — as the human knee after the flip.
        shank.ConnectTo(thigh,
            new Vector2(0f, shankSize.y * 0.5f),
            new Vector2(0f, -thighSize.y * 0.5f),
            0f, 130f);
        AddFriction(shank, kneeFriction, 0.8f);
        AddMuscles(shank, kneeMuscleTorque);

        float footHalf = footSize.x * 0.5f;
        float footCenterX = hipRoot.x - ankleHeelOffset + footHalf;
        float ankleAnchorX = hipRoot.x - footCenterX;
        float footY = hipRoot.y - thighSize.y - shankSize.y - footSize.y * 0.5f;

        HumanSegment foot = CreateSegment(sideName + "Foot", footSize, FOOT_MASS, footColor, footOrder, HumanVisualShape.Box, true);
        foot.transform.localPosition = new Vector3(footCenterX, footY, 0f);
        foot.SetVisualOffset(visualShift);
        foot.ConnectTo(shank,
            new Vector2(ankleAnchorX, footSize.y * 0.5f),
            new Vector2(0f, -shankSize.y * 0.5f),
            -40f, 40f);
        AddFriction(foot, ankleFriction, 0.5f);
        AddMuscles(foot, ankleFlexorTorque, ankleExtensorTorque);
    }

    private HumanSegment CreateSegment(
        string name,
        Vector2 size,
        float massFraction,
        Color color,
        int sortingOrder,
        HumanVisualShape visualShape = HumanVisualShape.Box,
        bool floorInertia = false,
        string albedoKey = null)
    {
        GameObject segmentObject = new GameObject(name);
        HumanSegment segment = segmentObject.AddComponent<HumanSegment>();
        float segmentMass = totalMass * massFraction;
        segment.Initialize(name, size, segmentMass, color, transform, sortingOrder + VisualOrderLift, visualShape, albedoKey);
        segment.jointMarkerDiameter = jointMarkerDiameter;
        if (segment.rb != null)
        {
            segment.rb.sleepMode = rig == BirdRig.Ragdoll
                ? RigidbodySleepMode2D.NeverSleep
                : RigidbodySleepMode2D.StartAwake;
            // Legs and wings: a 2D slice gives I ~ 10⁻⁶; otherwise SPD chokes the torque.
            if (floorInertia && minLimbInertia > 0f && segment.rb.inertia < minLimbInertia)
                segment.rb.inertia = minLimbInertia;
            if (name == "Body" && minBodyInertia > 0f && segment.rb.inertia < minBodyInertia)
                segment.rb.inertia = minBodyInertia;
        }
        return segment;
    }

    private void AddBirdFlight(HumanSegment body)
    {
        flight = body.gameObject.AddComponent<BirdFlight>();
    }

    private void AddBodyAero(HumanSegment body)
    {
        BodyAero aero = body.gameObject.AddComponent<BodyAero>();
        aero.frontalArea = bodySize.x * bodySize.y * 0.85f;
        // BirdFlight applies the force: one FixedUpdate per individual, not two.
        aero.generateForce = false;
        aero.enabled = false;
    }

    private void AddFriction(HumanSegment segment, float damping, float maxTorque)
    {
        if (segment.joint == null) return;
        JointFriction friction = segment.gameObject.AddComponent<JointFriction>();
        friction.damping = damping;
        friction.maxTorque = maxTorque;
    }

    private void AddMuscles(HumanSegment segment, float maxTorque)
    {
        AddMuscles(segment, maxTorque, maxTorque);
    }

    private void AddMuscles(HumanSegment segment, float flexorTorque, float extensorTorque)
    {
        if (segment.joint == null) return;
        Muscle flexor = segment.gameObject.AddComponent<Muscle>();
        flexor.maxTorque = flexorTorque;
        flexor.direction = 1f;
        flexor.activation = 0f;
        Muscle extensor = segment.gameObject.AddComponent<Muscle>();
        extensor.maxTorque = extensorTorque;
        extensor.direction = -1f;
        extensor.activation = 0f;
    }

    private void BindController(BirdController bc)
    {
        BindPair("RightWingHumerus", out bc.rightShoulderJoint, out bc.rightShoulderFlexor, out bc.rightShoulderExtensor);
        BindPair("LeftWingHumerus", out bc.leftShoulderJoint, out bc.leftShoulderFlexor, out bc.leftShoulderExtensor);
        BindPair("RightWingUlna", out bc.rightElbowJoint, out bc.rightElbowFlexor, out bc.rightElbowExtensor);
        BindPair("LeftWingUlna", out bc.leftElbowJoint, out bc.leftElbowFlexor, out bc.leftElbowExtensor);
        BindPair("RightWingHand", out bc.rightWristJoint, out bc.rightWristFlexor, out bc.rightWristExtensor);
        BindPair("LeftWingHand", out bc.leftWristJoint, out bc.leftWristFlexor, out bc.leftWristExtensor);
        BindPair("RightLegThigh", out bc.rightHipJoint, out bc.rightHipFlexor, out bc.rightHipExtensor);
        BindPair("LeftLegThigh", out bc.leftHipJoint, out bc.leftHipFlexor, out bc.leftHipExtensor);
        BindPair("RightLegShank", out bc.rightKneeJoint, out bc.rightKneeFlexor, out bc.rightKneeExtensor);
        BindPair("LeftLegShank", out bc.leftKneeJoint, out bc.leftKneeFlexor, out bc.leftKneeExtensor);
        BindPair("RightLegFoot", out bc.rightAnkleJoint, out bc.rightAnkleFlexor, out bc.rightAnkleExtensor);
        BindPair("LeftLegFoot", out bc.leftAnkleJoint, out bc.leftAnkleFlexor, out bc.leftAnkleExtensor);
        BindPair("Neck", out bc.neckJoint, out bc.neckFlexor, out bc.neckExtensor);
        BindPair("Head", out bc.headJoint, out bc.headFlexor, out bc.headExtensor);
        BindPair("Beak", out bc.beakJoint, out bc.beakFlexor, out bc.beakExtensor);
        BindPair("Tail", out bc.tailJoint, out bc.tailFlexor, out bc.tailExtensor);
    }

    // Beak is a real segment: the only collider that may hurt an enemy.
    private void AttachRagdollBeak(HumanSegment head)
    {
        float beakX = head.transform.localPosition.x + headSize.x * 0.5f + beakSize.x * 0.5f;
        float beakY = head.transform.localPosition.y - headSize.y * 0.12f;
        HumanSegment beak = CreateSegment("Beak", beakSize, BEAK_MASS, BeakColor, BEAK_ORDER);
        beak.transform.localPosition = new Vector3(beakX, beakY, 0f);
        beak.ConnectTo(head,
            new Vector2(-beakSize.x * 0.5f, 0f),
            new Vector2(headSize.x * 0.5f, -headSize.y * 0.12f),
            -20f, 20f);
        AddFriction(beak, beakFriction, 0.25f);
        AddMuscles(beak, beakMuscleTorque);
        if (beak.collider != null)
            beak.collider.isTrigger = true;
        beakSegment = beak;
        beakVisual = beak.transform;
        beakCollider = beak.collider;
    }

    // Flock: no extra Rigidbody2D. Trigger rides on the body via Look.
    private void AttachFlockBeak(Transform head)
    {
        Transform beak = CreateVisualPart("Beak", beakSize, BeakColor, BEAK_ORDER, head);
        beak.localPosition = new Vector3(headSize.x * 0.5f + beakSize.x * 0.5f, -headSize.y * 0.12f, 0f);
        BoxCollider2D col = beak.gameObject.AddComponent<BoxCollider2D>();
        col.size = beakSize;
        col.isTrigger = true;
        beakVisual = beak;
        beakCollider = col;
        beakSegment = null;
    }

    // Hen comb + wattle on Head Visual. Comb rides Peck dip and Walk
    // bob already on the head, nods on Alarm/Call shout
    // (`cryCombAmp`), nods on hen Walk hop (`walkCombAmp`),
    // nods on Walk hop-turn (`walkTurnCombAmp`), nods on
    // Sit hop-turn (`sitTurnCombAmp`), nods on Sit
    // look-down (`sitLookCombAmp`), nods on Sit
    // preen nibble (`sitPreenCombAmp`), nods on Sit
    // scratch kick (`sitScratchCombAmp`), nods on Sit
    // land-ruffle (`landSettleCombAmp`), nods on Peck
    // swallow (`peckCombAmp`), nods on Fly first-air
    // (`takeoffCombAmp`), nods on Fly/Glide flare
    // (`landCombAmp`), and nods on Alarm glance
    // (`alarmLookCombAmp`) — a glued crown over a live
    // plant / toss / climb / flare / glance wattle read as a statue comb.
    // Wattle hangs on shout (`cryWattleAmp`), on
    // Attack stoop (`stoopWattleAmp`), on hen Walk hop
    // (`walkWattleAmp`), on Sit look-down (`sitLookWattleAmp`), on
    // Sit preen nibble (`sitPreenWattleAmp`), on Sit scratch
    // kick (`sitScratchWattleAmp`), on Sit hop-turn
    // (`sitTurnWattleAmp`), on Walk hop-turn
    // (`walkTurnWattleAmp`), on Sit land-ruffle
    // (`landSettleWattleAmp`), on Peck swallow
    // (`peckWattleAmp`), on Fly first-air
    // (`takeoffWattleAmp`), on Fly/Glide flare
    // (`landWattleAmp`), and on Alarm glance
    // (`alarmLookWattleAmp`).
    // Crow stays unmarked.
    private void AttachHenMarks(Transform headBone)
    {
        if (headBone == null)
            return;
        Transform vis = headBone.Find("Visual");
        AttachHenMarks(headBone, vis);
    }

    private void AttachEye(Transform headBone)
    {
        if (headBone == null)
            return;
        Transform vis = headBone.Find("Visual");
        AttachEye(headBone, vis);
    }

    private void AttachCrop(Transform neckBone)
    {
        if (neckBone == null)
            return;
        Transform vis = neckBone.Find("Visual");
        AttachCrop(neckBone, vis);
    }

    private void AttachTongue(Transform beakBone)
    {
        if (beakBone == null)
            return;
        Transform vis = beakBone.Find("Visual");
        AttachTongue(beakBone, vis);
    }

    private void AttachHenMarks(Transform headBone, Transform headVis)
    {
        if (kind != BirdKind.Chicken)
            return;
        Transform parent = headVis != null ? headVis : headBone;
        if (parent == null)
            return;
        float hy = headSize.y;
        float hx = headSize.x;
        Vector3 combFrontPos = new Vector3(hx * 0.08f, hy * 0.52f, 0f);
        Vector2 combFrontSize = new Vector2(0.016f, 0.024f);
        combFrontRestLocal = combFrontPos;
        combFrontRestSize = combFrontSize;
        combFrontRenderer = MakeHenMark("CombFront", parent, HEAD_ORDER + 2, CombColor,
            combFrontSize, combFrontPos);
        Vector3 combBackPos = new Vector3(-hx * 0.16f, hy * 0.44f, 0f);
        Vector2 combBackSize = new Vector2(0.012f, 0.016f);
        combBackRestLocal = combBackPos;
        combBackRestSize = combBackSize;
        combBackRenderer = MakeHenMark("CombBack", parent, HEAD_ORDER + 2, CombColor,
            combBackSize, combBackPos);
        Vector3 wattlePos = new Vector3(hx * 0.28f, -hy * 0.46f, 0f);
        Vector2 wattleSize = new Vector2(0.012f, 0.018f);
        wattleRestLocal = wattlePos;
        wattleRestSize = wattleSize;
        wattleRenderer = MakeHenMark("Wattle", parent, HEAD_ORDER + 1, WattleColor,
            wattleSize, wattlePos);
    }

    // Dark pupil toward the beak. All kinds. Picture only — no collider.
    private void AttachEye(Transform headBone, Transform headVis)
    {
        Transform parent = headVis != null ? headVis : headBone;
        if (parent == null)
            return;
        float hy = headSize.y;
        float hx = headSize.x;
        Vector3 eyePos = new Vector3(hx * 0.22f, hy * 0.12f, 0f);
        eyeRestLocal = eyePos;
        eyeRenderer = MakeHenMark("Eye", parent, HEAD_ORDER + 1, EyeColor,
            new Vector2(0.010f, 0.008f), eyePos);
        // Same slot, one order up: covers the pupil. Starts off —
        // Attack LateUpdate enables it. Fly/Glide keep the stare.
        nictRenderer = MakeHenMark("Nict", parent, HEAD_ORDER + 2, NictColor,
            new Vector2(0.012f, 0.010f), eyePos);
        nictRenderer.enabled = false;
        // Child of the pupil: Walk / Peck / Sit-look already move the eye.
        // Starts off — Walk / Peck / Sit-preen / Sit-scratch / Sit
        // hop-turn LateUpdate enables it. Sit blink / Attack nict keep
        // their own lids. Picture only.
        lidRenderer = MakeHenMark("Lid", eyeRenderer.transform, HEAD_ORDER + 2, LidColor,
            new Vector2(0.011f, 0.0036f), new Vector3(0f, 0.0032f, 0f));
        lidRenderer.enabled = false;
    }

    // Gullet on Neck Visual. Brain already wrote Peck; swallow toss
    // used to leave a silent grey neck, so eat read as a nod with
    // nowhere for the bite. Alarm/Call shout reuses it (`cryCropAmp`)
    // — gape opened, the crop stayed off. Attack stoop reuses it
    // (`stoopCropAmp`) — gape, nict, tongue, and wattle were live,
    // the crop stayed off. Starts off — Peck / Cry / Attack
    // LateUpdate enables it. Picture only — no collider. Woodpecker
    // drum keeps the brace. Shout tongue is a sibling on the beak
    // (`cryTongueAmp`) — crop left an empty orange bill. Attack
    // stoop reuses that sibling (`stoopTongueAmp`). Comb stays
    // glued on stoop (contrast).
    private void AttachCrop(Transform neckBone, Transform neckVis)
    {
        Transform parent = neckVis != null ? neckVis : neckBone;
        if (parent == null)
            return;
        float ny = neckSize.y;
        float nx = neckSize.x;
        Vector3 cropPos = new Vector3(nx * 0.42f, -ny * 0.22f, 0f);
        Vector2 cropSize = new Vector2(0.010f, 0.008f);
        cropRestLocal = cropPos;
        cropRestSize = cropSize;
        cropRenderer = MakeHenMark("Crop", parent, NECK_ORDER + 1, CropColor,
            cropSize, cropPos);
        cropRenderer.enabled = false;
    }

    // Tongue in the gape. Brain already cried Alarm/Call; crop
    // and the 0.55 s open-bill were live, the orange box stayed
    // empty, so Flee still flashed a mute swallow. Attack stoop
    // reuses it (`stoopTongueAmp`) — gape opened, the bill stayed
    // empty. Dirt swoop reuses it (`swoopTongueAmp`) — gape already
    // parts on the dive, the orange box stayed empty. Child of
    // Beak Visual. Starts off — Cry / Attack / swoop LateUpdate
    // enables it. Picture only — no collider. Peck grab stays
    // shut. Glance 8° stays glance. Attack stoop hen wattle is a
    // sibling on Head Visual (`stoopWattleAmp`).
    private void AttachTongue(Transform beakBone, Transform beakVis)
    {
        Transform parent = beakVis != null ? beakVis : beakBone;
        if (parent == null)
            return;
        float by = beakSize.y;
        float bx = beakSize.x;
        Vector3 tonguePos = new Vector3(-bx * 0.06f, -by * 0.38f, 0f);
        Vector2 tongueSize = new Vector2(0.010f, 0.0032f);
        tongueRestLocal = tonguePos;
        tongueRestSize = tongueSize;
        tongueRenderer = MakeHenMark("Tongue", parent, BEAK_ORDER + 1, TongueColor,
            tongueSize, tonguePos);
        tongueRenderer.enabled = false;
    }

    private static SpriteRenderer MakeHenMark(
        string name, Transform parent, int order, Color color, Vector2 size, Vector3 localPos)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = LevelArt.White();
        sr.color = color;
        sr.drawMode = SpriteDrawMode.Sliced;
        sr.size = size;
        sr.sortingOrder = order + VisualOrderLift;
        SpriteLighting.ApplyLit(sr);
        return sr;
    }

    private void AttachHit()
    {
        Damageable life = GetComponent<Damageable>();
        if (life == null)
            life = gameObject.AddComponent<Damageable>();
        life.maxHealth = HitHealth;
        life.health = life.maxHealth;

        BeakStrike strike = GetComponent<BeakStrike>();
        if (strike == null)
            strike = gameObject.AddComponent<BeakStrike>();
        strike.damage = PeckDamage;
        strike.Bind(beakCollider);
    }

    private void BindPair(string childName, out HingeJoint2D joint, out Muscle flexor, out Muscle extensor)
    {
        joint = null;
        flexor = null;
        extensor = null;
        Transform t = transform.Find(childName);
        if (t == null) return;
        joint = t.GetComponent<HingeJoint2D>();
        Muscle[] muscles = t.GetComponents<Muscle>();
        flexor = System.Array.Find(muscles, m => m.direction == 1f);
        extensor = System.Array.Find(muscles, m => m.direction == -1f);
    }

    // Wood is not a wall. Birds fly through the crown. BirdPerch pads are
    // triggers; sit is an attach, not a solver contact.
    public static void GhostTreeWood(Bird[] birds, PlantTree tree)
    {
        if (tree == null)
            return;
        GhostTreeWood(birds, new[] { tree });
    }

    public static void GhostTreeWood(Bird[] birds, PlantTree[] trees)
    {
        if (birds == null || trees == null)
            return;

        List<Collider2D> birdCols = new List<Collider2D>(32);
        List<Collider2D> tmp = new List<Collider2D>(8);
        for (int i = 0; i < birds.Length; i++)
        {
            if (birds[i] == null)
                continue;
            tmp.Clear();
            birds[i].GetComponentsInChildren<Collider2D>(tmp);
            birdCols.AddRange(tmp);
        }

        if (birdCols.Count == 0)
            return;

        List<Collider2D> wood = new List<Collider2D>(32);
        for (int t = 0; t < trees.Length; t++)
        {
            PlantTree tree = trees[t];
            if (tree == null)
                continue;
            tree.EnsureBirdPerches();
            wood.Clear();
            tree.GetComponentsInChildren<Collider2D>(wood);
            for (int w = 0; w < wood.Count; w++)
            {
                Collider2D wc = wood[w];
                if (wc == null)
                    continue;
                BirdPerch pad = wc.GetComponent<BirdPerch>();
                if (pad != null)
                {
                    pad.MakeTrigger();
                    continue;
                }
                for (int b = 0; b < birdCols.Count; b++)
                {
                    if (birdCols[b] != null)
                        Physics2D.IgnoreCollision(wc, birdCols[b], true);
                }
            }
        }
    }

    private void DisableCollisionsBetweenSegments()
    {
        Collider2D[] allColliders = GetComponentsInChildren<Collider2D>();
        for (int i = 0; i < allColliders.Length; i++)
        {
            for (int j = i + 1; j < allColliders.Length; j++)
                Physics2D.IgnoreCollision(allColliders[i], allColliders[j], true);
        }
    }

    // Torso and head stay on BirdCore (the only solver fixtures on a flock crow).
    // Perch and beak are triggers on BirdLimb. Wings and legs have no collider.
    // Layer matrix drops limb-limb and limb-core between birds; no N² pairs.
    private void StampContactLayers()
    {
        BirdContactLayers.Ensure();
        if (!BirdContactLayers.Ready)
            return;

        if (bodyCollider != null)
            BirdContactLayers.ApplyCore(bodyCollider.gameObject);
        if (headCollider != null)
            BirdContactLayers.ApplyCore(headCollider.gameObject);

        // Flock has four fixtures. Stamp them by field — no child scan.
        if (rig == BirdRig.Flock)
        {
            if (perchCollider != null)
                BirdContactLayers.ApplyLimb(perchCollider.gameObject);
            if (beakCollider != null)
                BirdContactLayers.ApplyLimb(beakCollider.gameObject);
            // B-GROUND-1: the claw pads. Limb, so the matrix keeps them off the
            // bird's own core and the ground still sees them.
            if (leftClaw != null)
                BirdContactLayers.ApplyLimb(leftClaw.gameObject);
            if (rightClaw != null)
                BirdContactLayers.ApplyLimb(rightClaw.gameObject);
            return;
        }

        Collider2D[] cols = GetComponentsInChildren<Collider2D>(true);
        for (int i = 0; i < cols.Length; i++)
        {
            Collider2D col = cols[i];
            if (col == null || BirdContactLayers.IsCore(col.gameObject))
                continue;
            BirdContactLayers.ApplyLimb(col.gameObject);
        }
    }

    // Pad this bird last sat on (world). BindPerches reads it for homeX/homeY.
    public Vector2 LastSeatSlot { get; private set; }

    // SeatBirds writes the root transform. After physics has run,
    // Rigidbody2D.position can stay at the dirt spawn, so the flock
    // piles on one pad. Write the body RB here too.
    public void PlaceOnSlot(Vector2 slot, float face)
    {
        LastSeatSlot = slot;
        float y = slot.y;
        if (perchCollider != null)
        {
            float bottom = perchCollider.offset.y - perchCollider.size.y * 0.5f;
            y = slot.y - bottom;
        }
        else
            y = StandingRootOffset(slot.y);

        Vector2 bodyPos = new Vector2(slot.x, y);
        transform.position = bodyPos;
        Rigidbody2D rb = controller != null ? controller.BodyRb : null;
        if (rb == null)
        {
            Transform bodyT = transform.Find("Body");
            rb = bodyT != null ? bodyT.GetComponent<Rigidbody2D>() : null;
        }

        if (rb != null)
        {
            rb.position = bodyPos;
            rb.rotation = 0f;
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
        }

        if (controller != null)
        {
            controller.mode = BirdMode.Sit;
            controller.takeoffDelay = 0f;
            controller.SetFacing(face);
        }
    }

    // Sample pads across the crown X span. First-N of GetPerchSlots is
    // twigs-first and stacks the flock on one side of the apple.
    public static void SpreadSeat(Bird[] pack, Vector2[] slots)
    {
        if (pack == null || pack.Length == 0)
            return;

        int n = pack.Length;
        int slotN = slots != null ? slots.Length : 0;
        Vector2[] ordered = null;
        if (slotN > 0)
        {
            ordered = new Vector2[slotN];
            for (int i = 0; i < slotN; i++)
                ordered[i] = slots[i];
            System.Array.Sort(ordered, CompareSlotX);
        }

        for (int i = 0; i < n; i++)
        {
            Bird bird = pack[i];
            if (bird == null || !bird.WantsTreeSeat)
                continue;

            Vector2 slot;
            if (ordered != null && ordered.Length > 0)
            {
                if (ordered.Length >= n)
                    slot = ordered[(int)((i + 0.5f) * ordered.Length / n)];
                else
                {
                    slot = ordered[i % ordered.Length];
                    int stack = i / ordered.Length;
                    slot.x += (stack % 5 - 2) * 0.28f;
                    slot.y += (stack / 5) * 0.05f;
                }
            }
            else
            {
                Vector2 origin = (Vector2)bird.transform.position;
                float mid = (n - 1) * 0.5f;
                slot = new Vector2(origin.x + (i - mid) * 0.55f, origin.y + (i % 3) * 0.12f);
            }

            bird.PlaceOnSlot(slot, (i & 1) == 0 ? 1f : -1f);
        }
    }

    private static int CompareSlotX(Vector2 a, Vector2 b)
    {
        int c = a.x.CompareTo(b.x);
        return c != 0 ? c : a.y.CompareTo(b.y);
    }
}

// Same-species bus. Drive writes, drive reads. No Find, no alloc in Update.
// Drum: a perched woodpecker rising-edge pulse that only crows overlay. Not
// Alarm (hens would Flee). Crows take off once, then drumStartleCool.
// Mob: crows share one threat X so the pack dives together. Mob beats drum.
public static class BirdSocial
{
    public const int Cap = 32;

    public static float DrumX;
    public static float DrumUntil;
    public static float MobX;
    public static float MobUntil;

    private static readonly BirdDrive[] hens = new BirdDrive[Cap];
    private static int henN;
    private static readonly BirdDrive[] crows = new BirdDrive[Cap];
    private static int crowN;

    public static int HenCount => henN;
    public static int CrowDriveCount => crowN;

    public static void PulseDrum(float x, float now, float hold = 0.7f)
    {
        DrumX = x;
        float until = now + hold;
        if (until > DrumUntil)
            DrumUntil = until;
    }

    public static bool Drumming(float now, out float x)
    {
        x = DrumX;
        return now < DrumUntil;
    }

    public static void PulseMob(float x, float now, float hold)
    {
        MobX = x;
        float until = now + hold;
        if (until > MobUntil)
            MobUntil = until;
    }

    public static bool Mobbing(float now, out float x)
    {
        x = MobX;
        return now < MobUntil;
    }

    public static void AddHen(BirdDrive d)
    {
        Add(hens, ref henN, d);
    }

    public static void DropHen(BirdDrive d)
    {
        Drop(hens, ref henN, d);
    }

    public static void AddCrow(BirdDrive d)
    {
        Add(crows, ref crowN, d);
    }

    public static void DropCrow(BirdDrive d)
    {
        Drop(crows, ref crowN, d);
    }

    public static BirdDrive HenAt(int i)
    {
        if (i < 0 || i >= henN)
            return null;
        return hens[i];
    }

    private static void Add(BirdDrive[] list, ref int n, BirdDrive d)
    {
        if (d == null || n >= list.Length)
            return;
        for (int i = 0; i < n; i++)
        {
            if (list[i] == d)
                return;
        }

        list[n++] = d;
    }

    private static void Drop(BirdDrive[] list, ref int n, BirdDrive d)
    {
        for (int i = 0; i < n; i++)
        {
            if (list[i] != d)
                continue;
            for (int j = i; j < n - 1; j++)
                list[j] = list[j + 1];
            list[--n] = null;
            return;
        }
    }
}

// B-WING-4: the spread wing's feather fan (picture only). Each feather's own
// −Y is its length; a positive Z turn swings it toward the bone's trailing
// side. `SetOpen(0..1)` lerps every feather between the folded pose (on the
// bone, scaled down — the B-WING-3 rail) and its own fan angle. No physics,
// no forces, no RNG; the controller calls it from LateUpdate.
public sealed class WingFeatherFan
{
    // The fan's own fold pose: every feather lies along its bone (8 deg off the
    // bone's own axis, on its trailing side) at 85 % of its length — the
    // B-WING-6 laid-back fold. The plates then lie along the back with their
    // tips stepped toward the tail (the outermost past the tail's tip); the old
    // tuck to the bone root (172) piled them into a knot (the B-WING-3 rail is
    // superseded by the owner's word).
    private const float FoldDeg = 8f;
    private const float FoldScale = 0.85f;
    // B-WING-6 p3: the secondaries' own fold — tucked back along the forearm
    // toward the elbow (see Bird.FeatherFoldDegSec).
    private const float FoldDegSec = 172f;
    // Number of the leading plates that are the hand's primaries; the rest are
    // the forearm's secondaries and fold by FoldDegSec.
    private readonly int primaryCount;
    // B-WING-6: the folded pack narrows in X (0.45) so the long plates stack
    // into a slim sheaf — the old 0.62 read as rows of capsules.
    private const float FoldWidthScale = 0.45f;

    private readonly Transform[] feathers;
    private readonly float[] lengths;
    private readonly float[] openDeg;
    private readonly Transform[] roots;
    private readonly float[] rootLens;

    public float Open01 { get; private set; }

    public WingFeatherFan(Transform[] feathers, float[] lengths, float[] openDeg,
                          Transform[] roots, float[] rootLens, int primaryCount)
    {
        this.feathers = feathers;
        this.lengths = lengths;
        this.openDeg = openDeg;
        this.roots = roots;
        this.rootLens = rootLens;
        this.primaryCount = primaryCount;
        SetOpen(0f);
    }

    // The drive: 0 = folded along the bone, 1 = the wing's fan fully open.
    public void SetOpen(float open01)
    {
        float o = Mathf.Clamp01(open01);
        Open01 = o;
        if (feathers == null)
            return;
        for (int i = 0; i < feathers.Length; i++)
        {
            Transform f = feathers[i];
            if (f == null)
                continue;
            f.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(i < primaryCount ? FoldDeg : FoldDegSec, openDeg[i], o));
            f.localScale = new Vector3(Mathf.Lerp(FoldWidthScale, 1f, o), Mathf.Lerp(FoldScale, 1f, o), 1f);
        }
    }

    // The chord the fan currently shows, m: the largest perpendicular distance
    // from the feather tips to their own bone's axis. Folded it is the fan's
    // own few millimetres, open it is the wing's real width — the area the
    // rows can carry.
    public float Chord()
    {
        if (feathers == null)
            return 0f;
        float best = 0f;
        for (int i = 0; i < feathers.Length; i++)
        {
            Transform f = feathers[i];
            if (f == null || roots[i] == null)
                continue;
            Vector3 root = roots[i].position;
            Vector3 axis = roots[i].TransformPoint(0f, -rootLens[i], 0f) - root;
            float axisSq = axis.sqrMagnitude;
            if (axisSq < 1e-12f)
                continue;
            Vector3 rel = f.TransformPoint(0f, -lengths[i], 0f) - root;
            Vector3 perp = rel - axis * (Vector3.Dot(rel, axis) / axisSq);
            float d = perp.magnitude;
            if (d > best)
                best = d;
        }
        return best;
    }
}
