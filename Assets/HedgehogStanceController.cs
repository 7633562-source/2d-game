using UnityEngine;

// Quadruped crawl stance. Sibling of DogStanceController, not a subclass.
// Quiet pose is muscles. Crawl translation is an external force on the
// bodies; legs only wiggle the Visual child. No leap, no keyboard.
[DefaultExecutionOrder(0)]
public class HedgehogStanceController : MonoBehaviour
{
    public enum StanceState
    {
        Balancing,
        Recovery,
        Falling,
        Walk
    }

    [System.NonSerialized]
    public bool energyRest;

    [Header("State")]
    public StanceState currentState = StanceState.Balancing;
    public float recoveryCoMOffset = 0.12f;
    public float fallCoMOffset = 0.24f;

    [Header("CoM / paw")]
    public float comOffsetReference = 0.08f;
    public float comVelocityReference = 0.25f;
    public float pawComP = 12f;
    public float pawComD = 7f;
    public float pawLimitMargin = 4f;

    [Header("Chest world pitch (deg)")]
    public float chestPGain = 2f;
    public float chestDGain = 0.3f;
    public float chestTargetTilt = -4f;
    public float chestErrorReferenceDegrees = 25f;

    [Header("Pelvis height (deg per metre, not AddForce)")]
    public float heightP = 40f;
    public float heightD = 8f;
    public float heightMaxDeg = 16f;
    public float targetPelvisY;
    public bool hasHeightTarget;
    public float pelvisVelY;

    [Header("Limb joint pose")]
    public float hipPGain = 2f;
    public float hipDGain = 0.3f;
    public float startupHipP = 8f;
    public float startupHipSeconds = 0.25f;
    public float hipBaseAngle = -32f;
    public float shoulderBaseAngle = 32f;
    public float kneePGain = 3f;
    public float kneeDGain = 0.4f;
    public float kneeBaseAngle = 18f;
    public float elbowBaseAngle = 18f;
    public float tailPGain = 0.8f;
    public float tailDGain = 0.3f;
    public float tailBaseAngle = 0f;

    [Header("Jaw (deg). Plus opens, 0 is closed.")]
    public float jawPGain = 2.5f;
    public float jawDGain = 0.4f;
    public float jawClosedAngle = 0f;
    public float jawOpenAngle = 28f;
    [Range(0f, 1f)]
    public float jawOpen;
    public bool jawStrikeArmed;
    public float jawAngle;
    public float biteOpenHold = 0.14f;
    public float biteCloseHold = 0.22f;
    public float biteRestHold = 0.18f;

    [Header("Crawl (body force; legs are look)")]
    [Tooltip("Horizontal force on the whole mass, N. Translation is not a paw paddle.")]
    public float crawlForce = 2.4f;
    [Tooltip("Speed cap along the crawl goal, m/s.")]
    public float crawlSpeed = 0.32f;
    [Tooltip("Start slowing when |mouthX − goalX| is under this, m.")]
    public float crawlSlowDistance = 0.28f;
    [Tooltip("Seconds to close a speed error. Softer than a 200 Hz slam.")]
    public float crawlSpeedTau = 0.14f;
    [Tooltip("Look-step period, s. Drives Visual wiggle only.")]
    public float walkPeriod = 0.55f;
    public float wiggleHipAmp = 6f;
    public float wiggleShoulderAmp = 6f;
    public float wiggleKneeAmp = 9f;
    public float wiggleElbowAmp = 9f;
    public float walkActivationSpeed = 20f;
    public float walkFoldDrop = 0.12f;
    public float walkFoldPitch = 50f;
    public bool walkWanted;
    public float walkPhase;
    public int walkCycleCount;
    [Tooltip("+1 crawl toward +X, −1 toward −X. Drive writes this.")]
    public float crawlSign = 1f;
    [Tooltip("Chest P while crawling. Stiffer than stance so the shove cannot pitch the dome.")]
    public float crawlChestPGain = 8f;
    public float crawlChestDGain = 1.2f;
    public bool hasCrawlGoal;
    public float crawlGoalX;
    public bool feedWanted;
    public Vector2 feedPoint;
    public bool hasFeedPoint;

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
    private HumanSegment frontRightUpperSeg, frontLeftUpperSeg;
    private HumanSegment frontRightLowerSeg, frontLeftLowerSeg;
    private HumanSegment rearRightThighSeg, rearLeftThighSeg;
    private HumanSegment rearRightShinSeg, rearLeftShinSeg;
    private HumanSegment frontRightPawSeg, frontLeftPawSeg;
    private HumanSegment rearRightPawSeg, rearLeftPawSeg;
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
    private float frontRightPawInertia, frontLeftPawInertia;
    private float rearRightPawInertia, rearLeftPawInertia;
    private float prevPelvisY;
    private bool hasPrevPelvisY;
    private float spawnedFixedTime = -1f;
    private float biteOpenUntil = -1f;
    private float biteCloseUntil = -1f;
    private float biteRestUntil = -1f;
    private BoxCollider2D jawMouthCol;
    private float walkClockStart = -1f;
    private float activationSpeedThisStep;

    public void Bind(Hedgehog body)
    {
        vestibular = body != null ? body.vestibularSystem : GetComponent<VestibularSystem>();
        chestTf = body != null && body.chestSegment != null ? body.chestSegment.transform : transform.Find("Chest");
        pelvisTf = body != null && body.pelvisSegment != null ? body.pelvisSegment.transform : transform.Find("Pelvis");
        headTf = body != null && body.headSegment != null ? body.headSegment.transform : transform.Find("Head");
        jawTf = body != null && body.jawSegment != null ? body.jawSegment.transform : transform.Find("Jaw");
        if (body != null && body.jawCollider != null)
            jawMouthCol = body.jawCollider;
        else if (jawTf != null)
            jawMouthCol = jawTf.GetComponent<BoxCollider2D>();
        CacheBodies();
        BindJointsAndMuscles();
        spawnedFixedTime = Time.fixedTime;
    }

    private void CacheBodies()
    {
        bodies = GetComponentsInChildren<Rigidbody2D>();
        frontRightPawCol = Col("FrontRightPaw");
        frontLeftPawCol = Col("FrontLeftPaw");
        rearRightPawCol = Col("RearRightPaw");
        rearLeftPawCol = Col("RearLeftPaw");
        frontRightUpperSeg = Seg("FrontRightUpper");
        frontLeftUpperSeg = Seg("FrontLeftUpper");
        frontRightLowerSeg = Seg("FrontRightLower");
        frontLeftLowerSeg = Seg("FrontLeftLower");
        rearRightThighSeg = Seg("RearRightThigh");
        rearLeftThighSeg = Seg("RearLeftThigh");
        rearRightShinSeg = Seg("RearRightShin");
        rearLeftShinSeg = Seg("RearLeftShin");
        frontRightPawSeg = Seg("FrontRightPaw");
        frontLeftPawSeg = Seg("FrontLeftPaw");
        rearRightPawSeg = Seg("RearRightPaw");
        rearLeftPawSeg = Seg("RearLeftPaw");
        SeedSupportFromPawBounds();
    }

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

    private HumanSegment Seg(string childName)
    {
        Transform t = transform.Find(childName);
        return t != null ? t.GetComponent<HumanSegment>() : null;
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
        if (energyRest) return;
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

        if (walkWanted)
        {
            float drop = hasHeightTarget ? Mathf.Max(0f, targetPelvisY - pelvisY) : 0f;
            float absPitch = Mathf.Abs(chestPitch);
            if (drop > walkFoldDrop || (drop > 0.22f && absPitch > 50f) || absPitch > walkFoldPitch)
            {
                walkWanted = false;
                currentState = StanceState.Falling;
            }
            else
            {
                currentState = StanceState.Walk;
                AdvanceWalkClock();
            }
        }
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

        TryRecoverFromFall();
    }

    private void Drive()
    {
        activationSpeedThisStep = muscleActivationSpeed;

        if (currentState == StanceState.Falling)
        {
            RelaxAll();
            return;
        }

        bool crawl = IsWalk();
        if (crawl)
        {
            activationSpeedThisStep = walkActivationSpeed > 0f
                ? walkActivationSpeed
                : muscleActivationSpeed;
        }
        // Hold X on the berry even after EndWalk so the snout stays
        // under the fruit instead of drifting a jaw-length away.
        if (hasCrawlGoal || crawl)
            ApplyCrawlForce();

        if (!crawl)
        {
            float offsetN = comOffsetX / Mathf.Max(0.01f, comOffsetReference);
            float velN = comVelocity.x / Mathf.Max(0.05f, comVelocityReference);
            float pawBalance = Mathf.Clamp(pawComP * offsetN + pawComD * velN, -1f, 1f);
            DrivePaw(frontRightPawJoint, frontRightPawFlexor, frontRightPawExtensor, frontRightGrounded, pawBalance);
            DrivePaw(frontLeftPawJoint, frontLeftPawFlexor, frontLeftPawExtensor, frontLeftGrounded, pawBalance);
            DrivePaw(rearRightPawJoint, rearRightPawFlexor, rearRightPawExtensor, rearRightGrounded, pawBalance);
            DrivePaw(rearLeftPawJoint, rearLeftPawFlexor, rearLeftPawExtensor, rearLeftGrounded, pawBalance);
        }
        else
        {
            // Hold the planted pose. CoM-offset paw PD would fight the
            // crawl force; limp paws let the feet twist and lose the patch.
            ControlJointStable(frontRightPawJoint, 0f, hipPGain, hipDGain,
                frontRightPawFlexor, frontRightPawExtensor, ref frontRightPawInertia);
            ControlJointStable(frontLeftPawJoint, 0f, hipPGain, hipDGain,
                frontLeftPawFlexor, frontLeftPawExtensor, ref frontLeftPawInertia);
            ControlJointStable(rearRightPawJoint, 0f, hipPGain, hipDGain,
                rearRightPawFlexor, rearRightPawExtensor, ref rearRightPawInertia);
            ControlJointStable(rearLeftPawJoint, 0f, hipPGain, hipDGain,
                rearLeftPawFlexor, rearLeftPawExtensor, ref rearLeftPawInertia);
        }

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

        DriveLumbarToWorld(crawl ? 0f : chestTargetTilt, crawl);

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
        DriveHead();
    }

    void LateUpdate()
    {
        ApplyLegWiggle();
    }

    public void Walk()
    {
        if (currentState == StanceState.Falling)
            return;
        walkWanted = true;
        if (walkClockStart < 0f)
            walkClockStart = Time.fixedTime;
        if (!IsWalk())
            currentState = StanceState.Walk;
    }

    public void EndWalk()
    {
        walkWanted = false;
        if (IsWalk())
            currentState = StanceState.Balancing;
    }

    public bool IsWalk()
    {
        return currentState == StanceState.Walk;
    }

    public bool IsChewing => Time.fixedTime < biteRestUntil;

    public void Bite()
    {
        if (IsChewing)
            return;
        float now = Time.fixedTime;
        biteOpenUntil = now + Mathf.Max(0.04f, biteOpenHold);
        biteCloseUntil = biteOpenUntil + Mathf.Max(0.06f, biteCloseHold);
        biteRestUntil = biteCloseUntil + Mathf.Max(0.04f, biteRestHold);
    }

    public void SetCrawlGoal(float x, Vector2 feed)
    {
        crawlGoalX = x;
        hasCrawlGoal = true;
        feedPoint = feed;
        hasFeedPoint = true;
    }

    public void ClearCrawlGoal()
    {
        hasCrawlGoal = false;
        hasFeedPoint = false;
        feedWanted = false;
    }

    public Vector2 MouthPoint()
    {
        if (jawTf != null)
        {
            float along = 0.02f;
            if (jawMouthCol != null)
                along = jawMouthCol.size.x * 0.5f;
            return (Vector2)jawTf.position + (Vector2)jawTf.right * along;
        }
        if (headTf != null)
            return headTf.position;
        if (chestTf != null)
            return (Vector2)chestTf.position + new Vector2(0.12f, 0f);
        return transform.position;
    }

    private void AdvanceWalkClock()
    {
        float period = Mathf.Max(0.08f, walkPeriod);
        if (walkClockStart < 0f)
            walkClockStart = Time.fixedTime;
        float elapsed = Time.fixedTime - walkClockStart;
        float wrapped = elapsed / period;
        int cycles = Mathf.FloorToInt(wrapped);
        if (cycles > walkCycleCount)
            walkCycleCount = cycles;
        walkPhase = wrapped - cycles;
    }

    // Horizontal force on the mass, shared by mass so hinges do not see
    // a crawl couple. Apply at sole height: a force through CoM plus paw
    // friction is a nose-down couple F·h. Track crawlGoalX with a speed
    // cap so the snout arrives and stays, instead of a constant shove.
    private void ApplyCrawlForce()
    {
        int groundedN = (frontRightGrounded ? 1 : 0) + (frontLeftGrounded ? 1 : 0)
                        + (rearRightGrounded ? 1 : 0) + (rearLeftGrounded ? 1 : 0);
        if (groundedN < 1)
            return;
        if (bodies == null || bodies.Length == 0)
            CacheBodies();

        float mass = 0f;
        for (int i = 0; i < bodies.Length; i++)
        {
            Rigidbody2D rb = bodies[i];
            if (rb == null || rb.gameObject == gameObject)
                continue;
            mass += rb.mass;
        }
        if (mass < 0.01f)
            return;

        float err = CrawlErrorX();
        float maxV = Mathf.Max(0.05f, crawlSpeed);
        float slowBand = Mathf.Max(0.04f, crawlSlowDistance);
        float absErr = Mathf.Abs(err);
        if (absErr < slowBand)
            maxV *= Mathf.Max(0.08f, absErr / slowBand);
        float desiredVx = Mathf.Clamp(err / Mathf.Max(0.12f, crawlSpeedTau * 2.5f), -maxV, maxV);
        float dv = desiredVx - comVelocity.x;
        if (absErr < 0.012f && Mathf.Abs(comVelocity.x) < 0.03f)
            return;
        float tau = Mathf.Max(0.06f, crawlSpeedTau);
        float force = Mathf.Clamp(mass * dv / tau, -crawlForce, crawlForce);
        if (Mathf.Abs(force) < 0.02f)
            return;

        float supportY = SupportSoleY();
        for (int i = 0; i < bodies.Length; i++)
        {
            Rigidbody2D rb = bodies[i];
            if (rb == null || rb.gameObject == gameObject)
                continue;
            Vector2 at = new Vector2(rb.worldCenterOfMass.x, supportY);
            rb.AddForceAtPosition(new Vector2(force * (rb.mass / mass), 0f), at, ForceMode2D.Force);
        }
    }

    private float CrawlErrorX()
    {
        if (hasCrawlGoal)
            return crawlGoalX - MouthPoint().x;
        float sign = crawlSign >= 0f ? 1f : -1f;
        return sign * 1f;
    }

    // Paws planted and the dome not on its side: stand up again. Otherwise
    // Falling → RelaxAll leaves the body on the dirt in front of the bush.
    private void TryRecoverFromFall()
    {
        if (currentState != StanceState.Falling)
            return;
        int groundedN = (frontRightGrounded ? 1 : 0) + (frontLeftGrounded ? 1 : 0)
                        + (rearRightGrounded ? 1 : 0) + (rearLeftGrounded ? 1 : 0);
        if (groundedN < 3)
            return;
        if (Mathf.Abs(chestPitch) > 28f)
            return;
        if (Mathf.Abs(comOffsetX) > recoveryCoMOffset)
            return;
        if (hasHeightTarget && (targetPelvisY - pelvisY) > walkFoldDrop * 0.5f)
            return;
        currentState = StanceState.Balancing;
        if (walkWanted)
            currentState = StanceState.Walk;
    }

    private float SupportSoleY()
    {
        float y = 0f;
        int n = 0;
        AddSole(frontRightPawCol, frontRightGrounded, ref y, ref n);
        AddSole(frontLeftPawCol, frontLeftGrounded, ref y, ref n);
        AddSole(rearRightPawCol, rearRightGrounded, ref y, ref n);
        AddSole(rearLeftPawCol, rearLeftGrounded, ref y, ref n);
        if (n > 0)
            return y / n;
        return comPosition.y;
    }

    private static void AddSole(Collider2D col, bool grounded, ref float y, ref int n)
    {
        if (col == null || !grounded)
            return;
        y += col.bounds.min.y;
        n++;
    }

    // Picture only: rotate Visual on the limb segments. Colliders stay in
    // the rest pose, so the look-step cannot paddle the dirt.
    private void ApplyLegWiggle()
    {
        float hipAmp = 0f;
        float shoulderAmp = 0f;
        float kneeAmp = 0f;
        float elbowAmp = 0f;
        if (!energyRest && IsWalk() && currentState != StanceState.Falling)
        {
            float dir = crawlSign >= 0f ? 1f : -1f;
            hipAmp = wiggleHipAmp * dir;
            shoulderAmp = wiggleShoulderAmp * dir;
            kneeAmp = wiggleKneeAmp;
            elbowAmp = wiggleElbowAmp;
        }

        float w = walkPhase * (Mathf.PI * 2f);
        float sA = Mathf.Sin(w);
        float sB = Mathf.Sin(w + Mathf.PI);
        float kneeA = Mathf.Max(0f, sA);
        float kneeB = Mathf.Max(0f, sB);

        SetVisualSpin(frontRightUpperSeg, shoulderAmp * sA);
        SetVisualSpin(frontLeftUpperSeg, shoulderAmp * sB);
        SetVisualSpin(frontRightLowerSeg, elbowAmp * kneeA);
        SetVisualSpin(frontLeftLowerSeg, elbowAmp * kneeB);
        SetVisualSpin(rearRightThighSeg, hipAmp * sB);
        SetVisualSpin(rearLeftThighSeg, hipAmp * sA);
        SetVisualSpin(rearRightShinSeg, kneeAmp * kneeB);
        SetVisualSpin(rearLeftShinSeg, kneeAmp * kneeA);
        SetVisualSpin(frontRightPawSeg, -0.35f * elbowAmp * kneeA);
        SetVisualSpin(frontLeftPawSeg, -0.35f * elbowAmp * kneeB);
        SetVisualSpin(rearRightPawSeg, -0.35f * kneeAmp * kneeB);
        SetVisualSpin(rearLeftPawSeg, -0.35f * kneeAmp * kneeA);
    }

    private static void SetVisualSpin(HumanSegment segment, float deg)
    {
        if (segment == null || segment.visual == null)
            return;
        segment.visual.localRotation = Quaternion.Euler(0f, 0f, deg);
    }

    private void DriveJaw()
    {
        float open = Mathf.Clamp01(jawOpen);
        float now = Time.fixedTime;
        if (now < biteOpenUntil)
            open = 1f;
        else if (now < biteCloseUntil)
            open = 0f;

        jawStrikeArmed = now >= biteOpenUntil && now < biteCloseUntil;
        float target = Mathf.Lerp(jawClosedAngle, jawOpenAngle, open);
        ControlJointStable(jawJoint, target, jawPGain, jawDGain,
            jawFlexor, jawExtensor, ref jawInertia);
    }

    private void DriveHead()
    {
        if (vestibular == null)
            return;
        float look = neckTargetTilt;
        if ((feedWanted || IsChewing) && hasFeedPoint && headTf != null)
        {
            Vector2 d = feedPoint - (Vector2)headTf.position;
            look = Mathf.Atan2(d.y, Mathf.Abs(d.x) + 0.02f) * Mathf.Rad2Deg;
            look = Mathf.Clamp(look, -18f, 22f);
        }
        ControlSegmentToWorldUpright(
            vestibular.GetNeckTilt(), vestibular.GetNeckAngularVelocity(),
            neckJoint, neckFlexor, neckExtensor, ref neckEffectiveInertia, look);
        ControlSegmentToWorldUpright(
            vestibular.GetHeadTilt(), vestibular.GetHeadAngularVelocity(),
            headJoint, headFlexor, headExtensor, ref headEffectiveInertia, look);
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
        return hitCount > 0;
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

    private void DriveLumbarToWorld(float targetTilt, bool crawl)
    {
        if (lumbarFlexor == null || lumbarExtensor == null || vestibular == null) return;
        float tilt = vestibular.GetBodyTilt();
        float angVel = vestibular.GetBodyAngularVelocity();
        float p = crawl ? crawlChestPGain : chestPGain;
        float d = crawl ? crawlChestDGain : chestDGain;
        float error = Mathf.DeltaAngle(tilt, targetTilt) / Mathf.Max(1f, chestErrorReferenceDegrees);
        float speed = angVel / Mathf.Max(1f, speedReferenceDegPerSec);
        float signal = (-p) * error - (-d) * speed;
        signal = Mathf.Clamp(signal, -1f, 1f);
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
