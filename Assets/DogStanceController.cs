using UnityEngine;
using UnityEngine.InputSystem;

// Quadruped stance. Not BalanceController. Forces only via Muscle activations.
[DefaultExecutionOrder(0)]
public class DogStanceController : MonoBehaviour
{
    public enum StanceState
    {
        Balancing,
        Recovery,
        Falling,
        LeapCrouch,
        LeapPush,
        LeapAir,
        LeapBite
    }

    [Header("State")]
    public StanceState currentState = StanceState.Balancing;
    public float recoveryCoMOffset = 0.15f;
    public float fallCoMOffset = 0.30f;

    [Header("CoM / paw")]
    public float comOffsetReference = 0.10f;
    public float comVelocityReference = 0.30f;
    public float pawComP = 12f;
    public float pawComD = 7f;
    public float pawLimitMargin = 4f;

    [Header("Chest / pelvis world pitch (deg)")]
    public float chestPGain = 2f;
    public float chestDGain = 0.3f;
    public float chestTargetTilt = -8f;
    public float chestErrorReferenceDegrees = 25f;
    public float pelvisPGain = 2f;
    public float pelvisDGain = 0.3f;
    public float pelvisTargetTilt = 0f;
    public float pelvisErrorReferenceDegrees = 25f;
    public float lumbarPGain = 2f;
    public float lumbarDGain = 0.3f;
    // c40: same joint also holds pelvis world 0. 0 = chest-only (c39).
    public float lumbarPelvisP = 0f;
    public float lumbarPelvisD = 0.3f;
    // Capped restore toward lumbar +8 (chest −8, pelvis 0). Not c29 (toward 0).
    public float lumbarStopP = 0f;
    public float lumbarStopAddCap = 0.40f;
    public float lumbarRestP = 0f;
    public float lumbarRestAddCap = 0.40f;
    // 0 = off. c37 open-gate was a no-op (world already unloads after slam).
    public float lumbarOpenGateDeg = 0f;
    // c38: hold lumbar joint at +8 (chest −8 + pelvis 0). Not c25 (joint 0).
    public bool useLumbarJointRest = false;
    public float lumbarJointRestDeg = 8f;

    [Header("Pelvis height (deg per metre, not AddForce)")]
    // c41 zeroed this on the theory that at kneeBaseAngle 0 the lift
    // term is one-way (positive error does nothing, negative one folds
    // the column) and so is dead weight. That reasoning holds regardless
    // of rake, but c41's zero-rake pose itself lost the plant (see
    // Dog.spawnHipAngle) — reverted with it back to the c39 value, since
    // the two changes were not isolated on the stand. Re-zero only behind
    // its own numbers.
    public float heightP = 60f;
    public float heightD = 8f;
    public float heightMaxDeg = 22f;
    public float heightHipMaxDeg = 0f;
    public float startupHoldSeconds = 0f;
    // c26 off all Drive. Here only chest world waits; legs still hold.
    public float lumbarDelaySeconds = 0f;
    public float targetPelvisY;
    public bool hasHeightTarget;
    public float pelvisVelY;

    [Header("Limb joint pose")]
    public float hipPGain = 2f;
    public float hipDGain = 0.3f;
    // c20 full-time hipP 4 lost plant. c59: P 8 for 0.25 s holds the
    // rake through the slam (drop 0.237 → 0.193, plant 0.992). P 12
    // and a 0.50 s window both lost plant.
    public float startupHipP = 8f;
    public float startupHipSeconds = 0.25f;
    // Must match Dog.spawnHipAngle / spawnShoulderAngle. Planted leftover
    // is −25 / +25 (c39). Columns 0/0 lost the plant (c41, c52).
    public float hipBaseAngle = -25f;
    public float shoulderBaseAngle = 25f;
    public float kneePGain = 3f;
    public float kneeDGain = 0.4f;
    public float kneeBaseAngle = 0f;
    public float elbowBaseAngle = 0f;
    public float tailPGain = 0.8f;
    public float tailDGain = 0.3f;
    public float tailBaseAngle = 0f;

    [Header("Jaw (deg). Plus opens, 0 is closed.")]
    public float jawPGain = 2.5f;
    public float jawDGain = 0.4f;
    public float jawClosedAngle = 0f;
    public float jawOpenAngle = 35f;
    [Range(0f, 1f)]
    public float jawOpen;
    public bool jawStrikeArmed;
    public float jawAngle;

    [Header("Leap attack (muscles only, no AddForce)")]
    public float leapCrouchSeconds = 0.35f;
    public float leapPushSeconds = 0.18f;
    public float leapAirSeconds = 0.70f;
    public float leapBiteSeconds = 0.40f;
    public float leapCrouchKnee = 55f;
    public float leapCrouchHip = -20f;
    public float leapCrouchShoulder = 20f;
    public float leapChestCoil = -14f;
    public float leapChestLaunch = 10f;
    public float leapActivationSpeed = 70f;
    public float leapBiteRange = 0.45f;
    public float leapPawPlant = 0.85f;
    public Transform leapTarget;
    public int leapCount;

    [Header("Neck / head Stable PD")]
    public float neckPGain = 1.5f;
    public float neckDGain = 0.4f;
    public float neckTargetTilt = 0f;
    public float neckErrorReferenceDegrees = 25f;
    public bool useStablePd = true;

    [Header("Shared PD refs")]
    public float errorReferenceDegrees = 10f;
    public float speedReferenceDegPerSec = 200f;
    public float muscleActivationSpeed = 20f;

    [Header("Read-only sensors")]
    public Vector2 comPosition;
    public Vector2 comVelocity;
    public float comOffsetX;
    public float supportMinX;
    public float supportMaxX;
    public float supportWidth;
    public bool hasCom;
    public bool frontRightGrounded;
    public bool frontLeftGrounded;
    public bool rearRightGrounded;
    public bool rearLeftGrounded;
    public float chestPitch;
    public float pelvisTilt;
    public float lumbarAngle;
    public float chestY;
    public float pelvisY;
    public int probeCallCount;
    public int probeSaturatedCount;
    public int probeHitSum;

    public Muscle lumbarFlexor, lumbarExtensor;
    public Muscle neckFlexor, neckExtensor;
    public Muscle headFlexor, headExtensor;
    public Muscle tailFlexor, tailExtensor;
    public Muscle jawFlexor, jawExtensor;
    public Muscle frontRightShoulderFlexor, frontRightShoulderExtensor;
    public Muscle frontLeftShoulderFlexor, frontLeftShoulderExtensor;
    public Muscle frontRightElbowFlexor, frontRightElbowExtensor;
    public Muscle frontLeftElbowFlexor, frontLeftElbowExtensor;
    public Muscle rearRightHipFlexor, rearRightHipExtensor;
    public Muscle rearLeftHipFlexor, rearLeftHipExtensor;
    public Muscle rearRightKneeFlexor, rearRightKneeExtensor;
    public Muscle rearLeftKneeFlexor, rearLeftKneeExtensor;
    public Muscle frontRightPawFlexor, frontRightPawExtensor;
    public Muscle frontLeftPawFlexor, frontLeftPawExtensor;
    public Muscle rearRightPawFlexor, rearRightPawExtensor;
    public Muscle rearLeftPawFlexor, rearLeftPawExtensor;

    float lumbarEffectiveInertia;

    public HingeJoint2D lumbarJoint;
    public HingeJoint2D neckJoint;
    public HingeJoint2D headJoint;
    public HingeJoint2D tailJoint;
    public HingeJoint2D jawJoint;
    public HingeJoint2D frontRightShoulderJoint, frontLeftShoulderJoint;
    public HingeJoint2D frontRightElbowJoint, frontLeftElbowJoint;
    public HingeJoint2D rearRightHipJoint, rearLeftHipJoint;
    public HingeJoint2D rearRightKneeJoint, rearLeftKneeJoint;
    public HingeJoint2D frontRightPawJoint, frontLeftPawJoint;
    public HingeJoint2D rearRightPawJoint, rearLeftPawJoint;

    private VestibularSystem vestibular;
    private Transform chestTf;
    private Transform pelvisTf;
    private Transform headTf;
    private Transform jawTf;
    private Rigidbody2D[] bodies = System.Array.Empty<Rigidbody2D>();
    private Collider2D frontRightPawCol, frontLeftPawCol, rearRightPawCol, rearLeftPawCol;
    private readonly ContactPoint2D[] groundContacts = new ContactPoint2D[8];
    private ContactFilter2D groundContactFilter;
    private bool groundContactFilterReady;
    private Vector2 previousCom;
    private bool hasPreviousCom;
    private bool hasLastSupport;
    private float lastMinX;
    private float lastMaxX;
    private float cachedComFixedTime = -1f;
    private Vector2 cachedCom;
    private float neckEffectiveInertia;
    private float headEffectiveInertia;
    private float frontRightElbowInertia, frontLeftElbowInertia;
    private float rearRightKneeInertia, rearLeftKneeInertia;
    private float rearRightHipInertia, rearLeftHipInertia;
    private float frontRightShoulderInertia, frontLeftShoulderInertia;
    private float tailInertia;
    private float jawInertia;
    private float lumbarInertia;
    private float prevPelvisY;
    private bool hasPrevPelvisY;
    private float spawnedFixedTime = -1f;
    private OrganismVoice voice;
    private float barkUntil = -1f;
    private float biteOpenUntil = -1f;
    private float biteCloseUntil = -1f;
    private bool spaceWasDown;
    private bool fWasDown;
    private bool rWasDown;
    private float leapPhaseStart = -1f;
    private bool leapBiteStarted;
    private float activationSpeedThisStep;
    private Vector2 leapAimPoint;

    public void Bind(Dog dog)
    {
        vestibular = dog != null ? dog.vestibularSystem : GetComponent<VestibularSystem>();
        chestTf = dog != null && dog.chestSegment != null ? dog.chestSegment.transform : transform.Find("Chest");
        pelvisTf = dog != null && dog.pelvisSegment != null ? dog.pelvisSegment.transform : transform.Find("Pelvis");
        headTf = dog != null && dog.headSegment != null ? dog.headSegment.transform : transform.Find("Head");
        jawTf = dog != null && dog.jawSegment != null ? dog.jawSegment.transform : transform.Find("Jaw");
        CacheBodies();
        BindJointsAndMuscles();
        voice = GetComponent<OrganismVoice>();
        spawnedFixedTime = Time.fixedTime;
    }

    private void CacheBodies()
    {
        bodies = GetComponentsInChildren<Rigidbody2D>();
        frontRightPawCol = Col("FrontRightPaw");
        frontLeftPawCol = Col("FrontLeftPaw");
        rearRightPawCol = Col("RearRightPaw");
        rearLeftPawCol = Col("RearLeftPaw");
        SeedSupportFromPawBounds();
    }

    // First frames are airborne: one paw then makes a 10 cm island and
    // |CoM offset| jumps past 0.30 m. Seed the stance polygon so
    // "last support" exists before contact (same rule as human CoM).
    private void SeedSupportFromPawBounds()
    {
        bool found = false;
        float minX = 0f;
        float maxX = 0f;
        ExpandBounds(frontRightPawCol, ref found, ref minX, ref maxX);
        ExpandBounds(frontLeftPawCol, ref found, ref minX, ref maxX);
        ExpandBounds(rearRightPawCol, ref found, ref minX, ref maxX);
        ExpandBounds(rearLeftPawCol, ref found, ref minX, ref maxX);
        if (!found) return;
        lastMinX = minX;
        lastMaxX = maxX;
        hasLastSupport = true;
    }

    private static void ExpandBounds(Collider2D col, ref bool found, ref float minX, ref float maxX)
    {
        if (col == null) return;
        Bounds b = col.bounds;
        minX = found ? Mathf.Min(minX, b.min.x) : b.min.x;
        maxX = found ? Mathf.Max(maxX, b.max.x) : b.max.x;
        found = true;
    }

    private Collider2D Col(string childName)
    {
        Transform t = transform.Find(childName);
        return t != null ? t.GetComponent<Collider2D>() : null;
    }

    private void BindJointsAndMuscles()
    {
        BindPair("Chest", out lumbarJoint, out lumbarFlexor, out lumbarExtensor);
        BindPair("Neck", out neckJoint, out neckFlexor, out neckExtensor);
        BindPair("Head", out headJoint, out headFlexor, out headExtensor);
        BindPair("Tail", out tailJoint, out tailFlexor, out tailExtensor);
        BindPair("Jaw", out jawJoint, out jawFlexor, out jawExtensor);
        BindPair("FrontRightUpper", out frontRightShoulderJoint, out frontRightShoulderFlexor, out frontRightShoulderExtensor);
        BindPair("FrontLeftUpper", out frontLeftShoulderJoint, out frontLeftShoulderFlexor, out frontLeftShoulderExtensor);
        BindPair("FrontRightLower", out frontRightElbowJoint, out frontRightElbowFlexor, out frontRightElbowExtensor);
        BindPair("FrontLeftLower", out frontLeftElbowJoint, out frontLeftElbowFlexor, out frontLeftElbowExtensor);
        BindPair("RearRightThigh", out rearRightHipJoint, out rearRightHipFlexor, out rearRightHipExtensor);
        BindPair("RearLeftThigh", out rearLeftHipJoint, out rearLeftHipFlexor, out rearLeftHipExtensor);
        BindPair("RearRightShin", out rearRightKneeJoint, out rearRightKneeFlexor, out rearRightKneeExtensor);
        BindPair("RearLeftShin", out rearLeftKneeJoint, out rearLeftKneeFlexor, out rearLeftKneeExtensor);
        BindPair("FrontRightPaw", out frontRightPawJoint, out frontRightPawFlexor, out frontRightPawExtensor);
        BindPair("FrontLeftPaw", out frontLeftPawJoint, out frontLeftPawFlexor, out frontLeftPawExtensor);
        BindPair("RearRightPaw", out rearRightPawJoint, out rearRightPawFlexor, out rearRightPawExtensor);
        BindPair("RearLeftPaw", out rearLeftPawJoint, out rearLeftPawFlexor, out rearLeftPawExtensor);
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
        flexor = System.Array.Find(muscles, m => m != null && m.direction == 1f);
        extensor = System.Array.Find(muscles, m => m != null && m.direction == -1f);
    }

    void FixedUpdate()
    {
        Sense();
        Drive();
    }

    private void Sense()
    {
        comPosition = ComputeCoM();
        hasCom = true;
        if (hasPreviousCom)
            comVelocity = (comPosition - previousCom) / Time.fixedDeltaTime;
        else
            comVelocity = Vector2.zero;
        previousCom = comPosition;
        hasPreviousCom = true;

        frontRightGrounded = IsPawGrounded(frontRightPawCol);
        frontLeftGrounded = IsPawGrounded(frontLeftPawCol);
        rearRightGrounded = IsPawGrounded(rearRightPawCol);
        rearLeftGrounded = IsPawGrounded(rearLeftPawCol);

        if (TryGetSupportBounds(out float minX, out float maxX))
        {
            supportMinX = minX;
            supportMaxX = maxX;
            supportWidth = maxX - minX;
            comOffsetX = comPosition.x - (minX + maxX) * 0.5f;
        }
        else
        {
            supportWidth = 0f;
            comOffsetX = 0f;
        }

        if (vestibular != null)
        {
            chestPitch = vestibular.GetBodyTilt();
            pelvisTilt = vestibular.GetPelvisTilt();
        }
        lumbarAngle = lumbarJoint != null ? lumbarJoint.jointAngle : 0f;
        jawAngle = jawJoint != null ? jawJoint.jointAngle : 0f;

        chestY = chestTf != null ? chestTf.position.y : transform.position.y;
        pelvisY = pelvisTf != null ? pelvisTf.position.y : transform.position.y;
        if (hasPrevPelvisY)
            pelvisVelY = (pelvisY - prevPelvisY) / Time.fixedDeltaTime;
        else
            pelvisVelY = 0f;
        prevPelvisY = pelvisY;
        hasPrevPelvisY = true;
        if (!hasHeightTarget)
        {
            targetPelvisY = pelvisY;
            hasHeightTarget = true;
        }

        if (IsLeap())
            AdvanceLeap();
        else
        {
            float absOff = Mathf.Abs(comOffsetX);
            if (absOff > fallCoMOffset)
                currentState = StanceState.Falling;
            else if (absOff > recoveryCoMOffset)
                currentState = StanceState.Recovery;
            else
                currentState = StanceState.Balancing;
        }
    }

    private void Drive()
    {
        activationSpeedThisStep = muscleActivationSpeed;

        if (currentState == StanceState.Falling)
        {
            RelaxAll();
            return;
        }

        if (IsLeap())
        {
            DriveLeap();
            return;
        }

        if (startupHoldSeconds > 0f && spawnedFixedTime >= 0f
            && Time.fixedTime - spawnedFixedTime < startupHoldSeconds)
            return;

        float offsetN = comOffsetX / Mathf.Max(0.01f, comOffsetReference);
        float velN = comVelocity.x / Mathf.Max(0.05f, comVelocityReference);
        float pawBalance = Mathf.Clamp(pawComP * offsetN + pawComD * velN, -1f, 1f);

        DrivePaw(frontRightPawJoint, frontRightPawFlexor, frontRightPawExtensor, frontRightGrounded, pawBalance);
        DrivePaw(frontLeftPawJoint, frontLeftPawFlexor, frontLeftPawExtensor, frontLeftGrounded, pawBalance);
        DrivePaw(rearRightPawJoint, rearRightPawFlexor, rearRightPawExtensor, rearRightGrounded, pawBalance);
        DrivePaw(rearLeftPawJoint, rearLeftPawFlexor, rearLeftPawExtensor, rearLeftGrounded, pawBalance);

        // c13 plant-gated hip lift (8°) lost fourFeet (0.027). Reverted.
        // Height stays on knee/elbow only.
        float lift = HeightLiftDeg();
        float kneeTarget = Mathf.Clamp(kneeBaseAngle - lift, 0f, 120f);
        float elbowTarget = Mathf.Clamp(elbowBaseAngle - lift, 0f, 120f);

        float hipP = hipPGain;
        if (startupHipSeconds > 0f && spawnedFixedTime >= 0f
            && Time.fixedTime - spawnedFixedTime < startupHipSeconds
            && startupHipP > 0f)
            hipP = startupHipP;

        ControlJointStable(rearLeftHipJoint, hipBaseAngle, hipP, hipDGain,
            rearLeftHipFlexor, rearLeftHipExtensor, ref rearLeftHipInertia);
        ControlJointStable(rearRightHipJoint, hipBaseAngle, hipP, hipDGain,
            rearRightHipFlexor, rearRightHipExtensor, ref rearRightHipInertia);
        ControlJointStable(frontLeftShoulderJoint, shoulderBaseAngle, hipP, hipDGain,
            frontLeftShoulderFlexor, frontLeftShoulderExtensor, ref frontLeftShoulderInertia);
        ControlJointStable(frontRightShoulderJoint, shoulderBaseAngle, hipP, hipDGain,
            frontRightShoulderFlexor, frontRightShoulderExtensor, ref frontRightShoulderInertia);

        bool lumbarReady = lumbarDelaySeconds <= 0f || spawnedFixedTime < 0f
            || Time.fixedTime - spawnedFixedTime >= lumbarDelaySeconds;
        if (lumbarReady)
        {
            if (useLumbarJointRest)
                ControlJointStable(lumbarJoint, lumbarJointRestDeg, lumbarPGain, lumbarDGain,
                    lumbarFlexor, lumbarExtensor, ref lumbarInertia);
            else
                DriveLumbarToWorld(chestTargetTilt);
        }

        ControlJointStable(rearLeftKneeJoint, kneeTarget, kneePGain, kneeDGain,
            rearLeftKneeFlexor, rearLeftKneeExtensor, ref rearLeftKneeInertia);
        ControlJointStable(rearRightKneeJoint, kneeTarget, kneePGain, kneeDGain,
            rearRightKneeFlexor, rearRightKneeExtensor, ref rearRightKneeInertia);
        ControlJointStable(frontLeftElbowJoint, elbowTarget, kneePGain, kneeDGain,
            frontLeftElbowFlexor, frontLeftElbowExtensor, ref frontLeftElbowInertia);
        ControlJointStable(frontRightElbowJoint, elbowTarget, kneePGain, kneeDGain,
            frontRightElbowFlexor, frontRightElbowExtensor, ref frontRightElbowInertia);

        ControlJointStable(tailJoint, tailBaseAngle, tailPGain, tailDGain,
            tailFlexor, tailExtensor, ref tailInertia);

        DriveJaw();

        if (vestibular != null)
        {
            ControlSegmentToWorldUpright(
                vestibular.GetNeckTilt(), vestibular.GetNeckAngularVelocity(),
                neckJoint, neckFlexor, neckExtensor, ref neckEffectiveInertia, neckTargetTilt);
            ControlSegmentToWorldUpright(
                vestibular.GetHeadTilt(), vestibular.GetHeadAngularVelocity(),
                headJoint, headFlexor, headExtensor, ref headEffectiveInertia, neckTargetTilt);
        }
    }

    void Update()
    {
        Keyboard kb = Keyboard.current;
        if (kb == null)
            return;

        bool space = kb.spaceKey.isPressed;
        if (space && !spaceWasDown)
            Bark();
        spaceWasDown = space;

        bool f = kb.fKey.isPressed;
        if (f && !fWasDown)
            Bite();
        fWasDown = f;

        bool r = kb.rKey.isPressed;
        if (r && !rWasDown)
            LeapAttack(leapTarget);
        rWasDown = r;
    }

    // Open a crack and cry. Not a brain: Play / CLI / Inspector call this.
    public void Bark()
    {
        barkUntil = Time.fixedTime + 0.16f;
        if (voice != null)
            voice.Cry(VoiceKind.Call);
    }

    // Open then slam shut and arm JawStrike. Closing is minus jointAngle.
    public void Bite()
    {
        biteOpenUntil = Time.fixedTime + 0.10f;
        biteCloseUntil = Time.fixedTime + 0.36f;
    }

    // Crouch, push with the planted paws, fly at the mark, bite.
    // Not a brain and not AddForce: the launch is knee/elbow extension
    // while the feet are still on the ground.
    public void LeapAttack(Transform target = null)
    {
        // Sit on this body often trips Falling from CoM offset. R / CLI
        // must still start the leap; DriveLeap, not RelaxAll, owns the jump.
        if (IsLeap())
            return;
        if (target != null)
            leapTarget = target;
        ResolveLeapAim();
        currentState = StanceState.LeapCrouch;
        leapPhaseStart = Time.fixedTime;
        leapBiteStarted = false;
        leapCount++;
    }

    public bool IsLeap()
    {
        return currentState == StanceState.LeapCrouch
               || currentState == StanceState.LeapPush
               || currentState == StanceState.LeapAir
               || currentState == StanceState.LeapBite;
    }

    public Vector2 LeapAim => leapAimPoint;

    private void ResolveLeapAim()
    {
        if (leapTarget != null)
        {
            leapAimPoint = leapTarget.position;
            return;
        }

        DogPrey mark = FindFirstObjectByType<DogPrey>();
        if (mark != null)
        {
            leapTarget = mark.transform;
            leapAimPoint = leapTarget.position;
            return;
        }

        Vector2 chest = chestTf != null ? (Vector2)chestTf.position : (Vector2)transform.position;
        leapAimPoint = chest + new Vector2(1.2f, 0f);
    }

    private void AdvanceLeap()
    {
        ResolveLeapAim();
        float age = leapPhaseStart >= 0f ? Time.fixedTime - leapPhaseStart : 0f;

        switch (currentState)
        {
            case StanceState.LeapCrouch:
                if (age >= leapCrouchSeconds)
                    EnterLeapPhase(StanceState.LeapPush);
                break;
            case StanceState.LeapPush:
                if (AllPawsAirborne() || age >= leapPushSeconds)
                    EnterLeapPhase(StanceState.LeapAir);
                break;
            case StanceState.LeapAir:
                if (InLeapBiteRange() || age >= leapAirSeconds)
                {
                    EnterLeapPhase(StanceState.LeapBite);
                    TryStartLeapBite();
                }
                break;
            case StanceState.LeapBite:
                TryStartLeapBite();
                if (age >= leapBiteSeconds)
                    EndLeap();
                break;
        }
    }

    private void EnterLeapPhase(StanceState next)
    {
        currentState = next;
        leapPhaseStart = Time.fixedTime;
    }

    private void TryStartLeapBite()
    {
        if (leapBiteStarted)
            return;
        leapBiteStarted = true;
        Bite();
    }

    private void EndLeap()
    {
        float drop = hasHeightTarget ? Mathf.Max(0f, targetPelvisY - pelvisY) : 0f;
        // CoM leaving the paws is not a fall during the jump. Fold only
        // when the trunk has really settled onto the dirt.
        currentState = drop > 0.60f ? StanceState.Falling : StanceState.Balancing;
        leapPhaseStart = -1f;
    }

    private bool AllPawsAirborne()
    {
        return !frontRightGrounded && !frontLeftGrounded
               && !rearRightGrounded && !rearLeftGrounded;
    }

    private bool InLeapBiteRange()
    {
        return Vector2.Distance(MouthPoint(), leapAimPoint) < leapBiteRange;
    }

    private Vector2 MouthPoint()
    {
        if (jawTf != null)
            return jawTf.position;
        if (headTf != null)
            return headTf.position;
        if (chestTf != null)
            return (Vector2)chestTf.position + new Vector2(0.25f, 0f);
        return transform.position;
    }

    // Crouch → plant extension → stretch in the air → bite. No AddForce:
    // the launch is knee/elbow opening while paws still push the ground.
    private void DriveLeap()
    {
        ResolveLeapAim();

        float kneeTarget = 0f;
        float elbowTarget = 0f;
        float hipTarget = hipBaseAngle;
        float shoulderTarget = shoulderBaseAngle;
        float chestTarget = chestTargetTilt;
        float pawCommand = 0f;
        bool plantPush = false;
        bool lookAtPrey = false;

        switch (currentState)
        {
            case StanceState.LeapCrouch:
                kneeTarget = leapCrouchKnee;
                elbowTarget = leapCrouchKnee;
                hipTarget = leapCrouchHip;
                shoulderTarget = leapCrouchShoulder;
                chestTarget = leapChestCoil;
                break;
            case StanceState.LeapPush:
                activationSpeedThisStep = leapActivationSpeed;
                kneeTarget = 0f;
                elbowTarget = 0f;
                // Extension past the coil: hip plus throws the pelvis
                // cranial, shoulder minus is the front-column mirror.
                hipTarget = 12f;
                shoulderTarget = -12f;
                chestTarget = leapChestLaunch;
                pawCommand = leapPawPlant;
                plantPush = true;
                break;
            case StanceState.LeapAir:
            case StanceState.LeapBite:
                kneeTarget = 0f;
                elbowTarget = 0f;
                hipTarget = 12f;
                shoulderTarget = -12f;
                chestTarget = leapChestLaunch;
                lookAtPrey = true;
                break;
        }

        if (plantPush)
        {
            DrivePaw(frontRightPawJoint, frontRightPawFlexor, frontRightPawExtensor, frontRightGrounded, pawCommand);
            DrivePaw(frontLeftPawJoint, frontLeftPawFlexor, frontLeftPawExtensor, frontLeftGrounded, pawCommand);
            DrivePaw(rearRightPawJoint, rearRightPawFlexor, rearRightPawExtensor, rearRightGrounded, pawCommand);
            DrivePaw(rearLeftPawJoint, rearLeftPawFlexor, rearLeftPawExtensor, rearLeftGrounded, pawCommand);
        }
        else
        {
            float offsetN = comOffsetX / Mathf.Max(0.01f, comOffsetReference);
            float velN = comVelocity.x / Mathf.Max(0.05f, comVelocityReference);
            float pawBalance = Mathf.Clamp(pawComP * offsetN + pawComD * velN, -1f, 1f);
            DrivePaw(frontRightPawJoint, frontRightPawFlexor, frontRightPawExtensor, frontRightGrounded, pawBalance);
            DrivePaw(frontLeftPawJoint, frontLeftPawFlexor, frontLeftPawExtensor, frontLeftGrounded, pawBalance);
            DrivePaw(rearRightPawJoint, rearRightPawFlexor, rearRightPawExtensor, rearRightGrounded, pawBalance);
            DrivePaw(rearLeftPawJoint, rearLeftPawFlexor, rearLeftPawExtensor, rearLeftGrounded, pawBalance);
        }

        ControlJointStable(rearLeftHipJoint, hipTarget, hipPGain, hipDGain,
            rearLeftHipFlexor, rearLeftHipExtensor, ref rearLeftHipInertia);
        ControlJointStable(rearRightHipJoint, hipTarget, hipPGain, hipDGain,
            rearRightHipFlexor, rearRightHipExtensor, ref rearRightHipInertia);
        ControlJointStable(frontLeftShoulderJoint, shoulderTarget, hipPGain, hipDGain,
            frontLeftShoulderFlexor, frontLeftShoulderExtensor, ref frontLeftShoulderInertia);
        ControlJointStable(frontRightShoulderJoint, shoulderTarget, hipPGain, hipDGain,
            frontRightShoulderFlexor, frontRightShoulderExtensor, ref frontRightShoulderInertia);

        DriveLumbarToWorld(chestTarget);

        ControlJointStable(rearLeftKneeJoint, kneeTarget, kneePGain, kneeDGain,
            rearLeftKneeFlexor, rearLeftKneeExtensor, ref rearLeftKneeInertia);
        ControlJointStable(rearRightKneeJoint, kneeTarget, kneePGain, kneeDGain,
            rearRightKneeFlexor, rearRightKneeExtensor, ref rearRightKneeInertia);
        ControlJointStable(frontLeftElbowJoint, elbowTarget, kneePGain, kneeDGain,
            frontLeftElbowFlexor, frontLeftElbowExtensor, ref frontLeftElbowInertia);
        ControlJointStable(frontRightElbowJoint, elbowTarget, kneePGain, kneeDGain,
            frontRightElbowFlexor, frontRightElbowExtensor, ref frontRightElbowInertia);

        ControlJointStable(tailJoint, tailBaseAngle, tailPGain, tailDGain,
            tailFlexor, tailExtensor, ref tailInertia);

        DriveJaw();

        if (vestibular != null)
        {
            float worldTarget = lookAtPrey ? LookTiltTowardAim() : neckTargetTilt;
            ControlSegmentToWorldUpright(
                vestibular.GetNeckTilt(), vestibular.GetNeckAngularVelocity(),
                neckJoint, neckFlexor, neckExtensor, ref neckEffectiveInertia, worldTarget);
            ControlSegmentToWorldUpright(
                vestibular.GetHeadTilt(), vestibular.GetHeadAngularVelocity(),
                headJoint, headFlexor, headExtensor, ref headEffectiveInertia, worldTarget);
        }
    }

    private float LookTiltTowardAim()
    {
        Vector2 from = MouthPoint();
        float look = Mathf.Atan2(leapAimPoint.y - from.y, leapAimPoint.x - from.x) * Mathf.Rad2Deg;
        return Mathf.Clamp(look, -30f, 30f);
    }

    private void DriveJaw()
    {
        float open = Mathf.Clamp01(jawOpen);
        float now = Time.fixedTime;
        if (now < barkUntil)
            open = 1f;
        if (currentState == StanceState.LeapCrouch)
            open = Mathf.Max(open, 0.22f);
        if (now < biteOpenUntil)
            open = 1f;
        else if (now < biteCloseUntil)
            open = 0f;

        jawStrikeArmed = now >= biteOpenUntil && now < biteCloseUntil;
        float target = Mathf.Lerp(jawClosedAngle, jawOpenAngle, open);
        ControlJointStable(jawJoint, target, jawPGain, jawDGain,
            jawFlexor, jawExtensor, ref jawInertia);
    }

    private Vector2 ComputeCoM()
    {
        if (Time.fixedTime == cachedComFixedTime)
            return cachedCom;

        Vector2 sum = Vector2.zero;
        float mass = 0f;
        if (bodies == null || bodies.Length == 0)
            CacheBodies();
        for (int i = 0; i < bodies.Length; i++)
        {
            Rigidbody2D rb = bodies[i];
            if (rb == null || rb.gameObject == gameObject) continue;
            sum += rb.worldCenterOfMass * rb.mass;
            mass += rb.mass;
        }
        cachedCom = mass < 0.001f ? (Vector2)transform.position : sum / mass;
        cachedComFixedTime = Time.fixedTime;
        return cachedCom;
    }

    private bool TryGetSupportBounds(out float minX, out float maxX)
    {
        minX = 0f;
        maxX = 0f;
        bool found = false;
        ExpandIfGrounded(frontRightPawCol, frontRightGrounded, ref found, ref minX, ref maxX);
        ExpandIfGrounded(frontLeftPawCol, frontLeftGrounded, ref found, ref minX, ref maxX);
        ExpandIfGrounded(rearRightPawCol, rearRightGrounded, ref found, ref minX, ref maxX);
        ExpandIfGrounded(rearLeftPawCol, rearLeftGrounded, ref found, ref minX, ref maxX);
        if (found)
        {
            lastMinX = minX;
            lastMaxX = maxX;
            hasLastSupport = true;
            return true;
        }
        if (hasLastSupport)
        {
            minX = lastMinX;
            maxX = lastMaxX;
            return true;
        }
        return false;
    }

    private static void ExpandIfGrounded(Collider2D col, bool grounded, ref bool found, ref float minX, ref float maxX)
    {
        if (col == null || !grounded) return;
        Bounds b = col.bounds;
        minX = found ? Mathf.Min(minX, b.min.x) : b.min.x;
        maxX = found ? Mathf.Max(maxX, b.max.x) : b.max.x;
        found = true;
    }

    private bool IsPawGrounded(Collider2D col)
    {
        if (col == null) return false;
        if (!groundContactFilterReady)
        {
            groundContactFilter = new ContactFilter2D();
            groundContactFilter.SetLayerMask(GroundLayers.Mask);
            groundContactFilter.useTriggers = false;
            groundContactFilterReady = true;
        }
        int hitCount = col.GetContacts(groundContactFilter, groundContacts);
        probeCallCount++;
        probeHitSum += hitCount;
        if (hitCount >= groundContacts.Length)
            probeSaturatedCount++;
        return hitCount > 0;
    }

    public bool AllPawsGrounded()
    {
        return frontRightGrounded && frontLeftGrounded && rearRightGrounded && rearLeftGrounded;
    }

    public float MaxStanceActivation()
    {
        float max = 0f;
        IncludeActivation(ref max, rearLeftHipFlexor);
        IncludeActivation(ref max, rearLeftHipExtensor);
        IncludeActivation(ref max, rearRightHipFlexor);
        IncludeActivation(ref max, rearRightHipExtensor);
        IncludeActivation(ref max, rearLeftKneeFlexor);
        IncludeActivation(ref max, rearLeftKneeExtensor);
        IncludeActivation(ref max, rearRightKneeFlexor);
        IncludeActivation(ref max, rearRightKneeExtensor);
        IncludeActivation(ref max, frontLeftShoulderFlexor);
        IncludeActivation(ref max, frontLeftShoulderExtensor);
        IncludeActivation(ref max, frontRightShoulderFlexor);
        IncludeActivation(ref max, frontRightShoulderExtensor);
        IncludeActivation(ref max, frontLeftElbowFlexor);
        IncludeActivation(ref max, frontLeftElbowExtensor);
        IncludeActivation(ref max, frontRightElbowFlexor);
        IncludeActivation(ref max, frontRightElbowExtensor);
        return max;
    }

    private static void IncludeActivation(ref float max, Muscle muscle)
    {
        if (muscle != null && muscle.activation > max)
            max = muscle.activation;
    }

    private void DrivePaw(HingeJoint2D joint, Muscle flexor, Muscle extensor, bool grounded, float command)
    {
        if (!grounded)
        {
            UpdateMuscle(flexor, 0f);
            UpdateMuscle(extensor, 0f);
            return;
        }
        if (joint == null || flexor == null || extensor == null) return;
        ApplySignal(ProtectJointLimit(joint, Mathf.Clamp(command, -1f, 1f)), flexor, extensor);
    }

    // targetPelvisY − pelvisY > 0 → pelvis too low → straighten
    // plus-fold joints only (knee/elbow). Hips stay the pose.
    private float HeightLiftDeg()
    {
        if (!hasHeightTarget) return 0f;
        bool anyPaw = frontRightGrounded || frontLeftGrounded
                      || rearRightGrounded || rearLeftGrounded;
        if (!anyPaw) return 0f;
        float err = targetPelvisY - pelvisY;
        float raw = heightP * err - heightD * pelvisVelY;
        return Mathf.Clamp(raw, -heightMaxDeg, heightMaxDeg);
    }

    private void DriveLumbarToWorld(float targetTilt)
    {
        if (lumbarFlexor == null || lumbarExtensor == null || vestibular == null) return;
        float tilt = vestibular.GetBodyTilt();
        float angVel = vestibular.GetBodyAngularVelocity();
        float error = Mathf.DeltaAngle(tilt, targetTilt) / Mathf.Max(1f, chestErrorReferenceDegrees);
        float speed = angVel / Mathf.Max(1f, speedReferenceDegPerSec);
        float signal = (-lumbarPGain) * error - (-lumbarDGain) * speed;
        if (lumbarPelvisP > 0f && vestibular != null)
        {
            float pErr = Mathf.DeltaAngle(vestibular.GetPelvisTilt(), pelvisTargetTilt)
                         / Mathf.Max(1f, pelvisErrorReferenceDegrees);
            float pSpd = vestibular.GetPelvisAngularVelocity()
                         / Mathf.Max(1f, speedReferenceDegPerSec);
            signal += lumbarPelvisP * pErr - lumbarPelvisD * pSpd;
        }
        signal = Mathf.Clamp(signal, -1f, 1f);
        if (lumbarJoint != null && lumbarRestP > 0f)
        {
            float rest = -targetTilt;
            float add = lumbarRestP * (rest - lumbarJoint.jointAngle) / 20f;
            signal += Mathf.Clamp(add, -lumbarRestAddCap, lumbarRestAddCap);
            signal = Mathf.Clamp(signal, -1f, 1f);
        }
        if (lumbarOpenGateDeg > 0f && lumbarJoint != null
            && lumbarJoint.jointAngle > lumbarOpenGateDeg && signal > 0f)
            signal = 0f;
        ApplySignal(ProtectJointLimit(lumbarJoint, signal), lumbarFlexor, lumbarExtensor);
    }

    private void ControlSegmentToWorldUpright(float tilt, float angVel, HingeJoint2D joint,
                                              Muscle flexor, Muscle extensor, ref float cachedInertia,
                                              float worldTarget)
    {
        if (flexor == null || extensor == null) return;
        float tiltForError = tilt;
        float damping = 0f;
        if (useStablePd)
        {
            float dt = Time.fixedDeltaTime;
            tiltForError = tilt + angVel * dt;
            if (cachedInertia <= 0f)
                cachedInertia = EffectiveInertia(joint);
            if (cachedInertia > 0f)
            {
                float k = neckDGain * flexor.maxTorque * Mathf.Rad2Deg
                          / Mathf.Max(1f, speedReferenceDegPerSec);
                damping = k * dt / cachedInertia;
            }
        }
        float error = Mathf.DeltaAngle(tiltForError, worldTarget) / Mathf.Max(1f, neckErrorReferenceDegrees);
        float speed = angVel / Mathf.Max(1f, speedReferenceDegPerSec);
        float raw = (-neckPGain) * error - (-neckDGain) * speed;
        float signal = Mathf.Clamp(raw / (1f + damping), -1f, 1f);
        ApplySignal(signal, flexor, extensor);
    }

    private void ControlJointStable(HingeJoint2D joint, float targetAngle,
                                    float pGain, float dGain,
                                    Muscle flexor, Muscle extensor, ref float cachedInertia)
    {
        if (joint == null || flexor == null || extensor == null) return;
        float dt = Time.fixedDeltaTime;
        float angleForError = joint.jointAngle + joint.jointSpeed * dt;
        float damping = 0f;
        if (cachedInertia <= 0f)
            cachedInertia = EffectiveInertia(joint);
        if (cachedInertia > 0f)
        {
            float k = dGain * flexor.maxTorque * Mathf.Rad2Deg
                      / Mathf.Max(1f, speedReferenceDegPerSec);
            damping = k * dt / cachedInertia;
        }
        float error = Mathf.DeltaAngle(angleForError, targetAngle) / Mathf.Max(1f, errorReferenceDegrees);
        float speed = joint.jointSpeed / speedReferenceDegPerSec;
        float raw = pGain * error - dGain * speed;
        ApplySignal(Mathf.Clamp(raw / (1f + damping), -1f, 1f), flexor, extensor);
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

    private void ApplySignal(float signal, Muscle flexor, Muscle extensor)
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

    private float ProtectJointLimit(HingeJoint2D joint, float signal)
    {
        if (joint == null || !joint.useLimits) return signal;
        float angle = joint.jointAngle;
        float min = joint.limits.min;
        float max = joint.limits.max;
        if (signal > 0f && angle >= max - pawLimitMargin) return 0f;
        if (signal < 0f && angle <= min + pawLimitMargin) return 0f;
        return signal;
    }

    private void UpdateMuscle(Muscle muscle, float targetActivation)
    {
        if (muscle == null) return;
        float speed = activationSpeedThisStep > 0f ? activationSpeedThisStep : muscleActivationSpeed;
        muscle.activation = Mathf.MoveTowards(
            muscle.activation, targetActivation,
            speed * Time.fixedDeltaTime);
    }

    private void RelaxAll()
    {
        UpdateMuscle(lumbarFlexor, 0f);
        UpdateMuscle(lumbarExtensor, 0f);
        UpdateMuscle(neckFlexor, 0f);
        UpdateMuscle(neckExtensor, 0f);
        UpdateMuscle(headFlexor, 0f);
        UpdateMuscle(headExtensor, 0f);
        UpdateMuscle(tailFlexor, 0f);
        UpdateMuscle(tailExtensor, 0f);
        UpdateMuscle(jawFlexor, 0f);
        UpdateMuscle(jawExtensor, 0f);
        UpdateMuscle(frontRightShoulderFlexor, 0f);
        UpdateMuscle(frontRightShoulderExtensor, 0f);
        UpdateMuscle(frontLeftShoulderFlexor, 0f);
        UpdateMuscle(frontLeftShoulderExtensor, 0f);
        UpdateMuscle(frontRightElbowFlexor, 0f);
        UpdateMuscle(frontRightElbowExtensor, 0f);
        UpdateMuscle(frontLeftElbowFlexor, 0f);
        UpdateMuscle(frontLeftElbowExtensor, 0f);
        UpdateMuscle(rearRightHipFlexor, 0f);
        UpdateMuscle(rearRightHipExtensor, 0f);
        UpdateMuscle(rearLeftHipFlexor, 0f);
        UpdateMuscle(rearLeftHipExtensor, 0f);
        UpdateMuscle(rearRightKneeFlexor, 0f);
        UpdateMuscle(rearRightKneeExtensor, 0f);
        UpdateMuscle(rearLeftKneeFlexor, 0f);
        UpdateMuscle(rearLeftKneeExtensor, 0f);
        UpdateMuscle(frontRightPawFlexor, 0f);
        UpdateMuscle(frontRightPawExtensor, 0f);
        UpdateMuscle(frontLeftPawFlexor, 0f);
        UpdateMuscle(frontLeftPawExtensor, 0f);
        UpdateMuscle(rearRightPawFlexor, 0f);
        UpdateMuscle(rearRightPawExtensor, 0f);
        UpdateMuscle(rearLeftPawFlexor, 0f);
        UpdateMuscle(rearLeftPawExtensor, 0f);
    }
}
