using UnityEngine;

public partial class StepPhaseDriver
{
    private MotionIntent intent;
    private BodyStateEstimator bodyState;
    private CenterOfMassCalculator comCalculator;
    private BalanceController balance;
    private VestibularSystem vestibular;

    private float walkStartTime = -1f;
    private bool pendingBegin;
    private Phase phase;
    private FallCatchPhase fallCatchPhase;
    private float phaseEnterTime;
    private float lastTickTime;
    private float currentStance;
    // Stance: whether the swing had at least one !grounded frame (for heel-strike).
    private bool swingWasAirborne;
    private float swingAirStreak;
    private bool swingAirQualified;
    private float prevTickTime = -1f;
    // simTime of the first Plant tick with the Plant→Fall gate shut, −1 while
    // the gate is open. The stall age is a stamp difference on purpose:
    // TickFallCatchWalk has no dt (prevTickTime is only stamped there).
    private float plantGateStallSince = -1f;
    // One re-seat per stall, so a shut gate cannot ping-pong support.
    private bool plantReseated;
    // Last rung of the ladder: Plant writes standLeg 0 (two-foot quiet
    // stance) until the gate opens again.
    private bool plantStandSurrender;
    // CoM height of the last pose that passed the gate on two feet, and
    // whether such a pose has been seen at all this walk.
    private float standComHeight;
    private bool standComHeightValid;
    // Standing CoM y, once, after BuildHuman while idle. Floor for the
    // sink guard so a folded BeginWalk cannot write its own collapsed
    // height as "this is how tall standing is". Not the pelvis spawn
    // column: that is ~0.28 m above live CoM (leg mass under the pelvis)
    // and exceeds sinkAbortDrop 0.25 on a quiet stance.
    private float spawnStandHeight;
    private bool spawnStandHeightValid;
    private float spawnPelvisY;
    private bool spawnPelvisYValid;
    private Transform pelvisTf;
    private Human humanBody;

    // Feet closer than this along the walk direction are "level": keep the
    // alternating order instead of flipping roles on contact noise.
    private const float FootLeadDeadzone = 0.02f;
    // Touchdown is an event; one physics step of contact is enough, but not
    // the same frame we entered Recover in.
    private const float RecoverTouchdownMinAge = 0.05f;
    // walk2_blend: TiltSettled was tilt-only (|tilt| < 45°). Recover planted
    // at 7° / 55 deg/s; 0.05 s later Fall at +49 deg/s and Catch cycle 1
    // went to 101°. Spin must settle too. 40 deg/s sits inside the recover
    // blend omegaRef (50) so Fall starts after the brake has begun to ease,
    // not while hard is still 1.
    private const float RecoverOmegaSettledDeg = 40f;
    // Trunk band that counts as "standing again" before the next fall.
    private const float TiltSettledMaxDeg = 12f;
    // Cycle-0 |CoM − stance| past this is already BalanceController Falling
    // (fallCoMOffset 0.30). The 3 s Plant stall is a collapse, not a restore.
    // Later cycles keep signed lead: a catch plants ~0.13 m ahead of CoM.
    private const float RecoverComLostMeters = 0.30f;

    void Awake()
    {
        // BodyState / CoM appear in BuildHuman after AddComponent —
        // they are not here yet. Pick them up lazily in EnsureRefs.
        intent = GetComponent<MotionIntent>();
    }

    void Start()
    {
        // AddComponent runs Start before Human.BuildHuman, so Pelvis is
        // missing here. Capture is retried from FixedUpdate while idle and
        // from BeginWalk before walkActive, never from a folded Tick.
        EnsureRefs();
        TryCaptureSpawnStandHeight();
    }
}
