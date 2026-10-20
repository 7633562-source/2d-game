using UnityEngine;

public partial class StepPhaseDriver
{
    // Plant-gated Catch age: 0 while the heel is up. Yield, arm decay,
    // and leftover shin extend read this so the trailing vault, the
    // Reach arm envelope, and the landing fold do not collapse in the air.
    public float CatchPlantAge()
    {
        if (catchPlantEnterTime < 0f)
            return 0f;
        return Mathf.Max(0f, lastTickTime - catchPlantEnterTime);
    }
    public float ScrapeResumeAirAge()
    {
        if (scrapeResumeAirEnterTime < 0f)
            return 0f;
        return Mathf.Max(0f, lastTickTime - scrapeResumeAirEnterTime);
    }

    // Last + lift-off age on the ReachExtendProgress slope. scrapeExtend
    // already counted dirt as air, so live Progress would jump on the
    // first air frame. scrapeResumeExtendStart is Catch-enter Last frozen
    // at lift-off; this does not compound if Tick writes Last each step.
    public float ScrapeResumeExtendProgress()
    {
        if (scrapeResumeAirEnterTime < 0f)
            return Mathf.Clamp01(lastReachExtendProgress);
        float window = ActiveReachTimeout();
        float start = Mathf.Clamp01(reachExtendStart);
        float denom = Mathf.Max(0.05f, 1f - start) * window;
        return Mathf.Clamp01(
            scrapeResumeExtendStart
            + ScrapeResumeAirAge() / Mathf.Max(0.02f, denom));
    }

    // Reach window actually in force: the stand overrides reachTimeout, the
    // cap does not come from the stand.
    public float ActiveReachTimeout() => Mathf.Min(
        Mathf.Max(0.05f, reachTimeout),
        Mathf.Max(0.05f, reachMaxDuration));
    // 0 = shin still folded, 1 = shin at reachKneeLandDeg. BalanceController
    // reads this to extend the swing knee before touchdown.
    public float ReachExtendProgress()
    {
        if (reachAirEnterTime < 0f)
            return 0f;
        // A contact flicker starts the air clock. Extending toward
        // reachKneeLandDeg while the WHOLE sole is down pins it (140° → 35°
        // on the ground, swingFoot 0.96). Hold that fold on a real plant.
        // A toe scrape is not a plant (fallSwingHeelPlant): resetting to 0
        // there put the ankle too high for airHeel to load the heel, so
        // Catch never saw plantedAfterAir. scrapeExtend keeps the clock.
        bool holdFold = IsSwingFootGrounded();
        if (fallSwingScrapeExtend > 0.5f && HeelPlantOn())
            holdFold = IsSwingHeelLoaded();
        if (holdFold)
            return 0f;
        float window = ActiveReachTimeout();
        float start = Mathf.Clamp01(reachExtendStart);
        float age = Mathf.Max(0f, lastTickTime - reachAirEnterTime) / window;
        // Airborne: start the shin extend immediately. reachExtendStart
        // is a grounded-Reach guard (do not unfold a still-loaded trail
        // toward landDeg). Once the foot is up, waiting 0.25 of timeout
        // holds reachKneeFlex and the sole stays behind CoM while the
        // trunk falls. Cycle-0 last-write (ApplyFirstStepCycle0Pose)
        // still Lerps 55° → landDeg; this only removes the wait.
        if (!IsSwingFootGrounded())
            start = 0f;
        return Mathf.Clamp01((age - start) / Mathf.Max(0.05f, 1f - start));
    }

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
    private float lastFallDuration;
    private float lastCatchDuration;
    private float lastCatchPlantDuration;
    private float catchPlantEnterTime = -1f;
    private float lastReachDuration;
    private float lastReachExtendProgress;
    private float lastRecoverDuration;
    private float lastRecoverStanceSign;
    private float lastPlantStanceShinDeg;
    private float reachEntryStanceComOffset;
    private float catchEntrySwingClearance;
    // Clock for shin extend. PhaseAge includes grounded Fall leftover, and
    // a cadence pass started ReachExtendProgress on that clock: the knee
    // ran toward reachKneeLandDeg while the foot was still down, so 140°
    // of flex never lifted (swingFoot 0.96). Start the window on first air.
    private float reachAirEnterTime = -1f;
    // First !grounded after a scrape resume (Catch→Reach or Catch
    // scrapeExtendAirAge). scrapeLiftAirAge / scrapeVaultAirAge /
    // scrapeArmAirAge extraAge is lastTickTime minus this, not PhaseAge
    // (dirt) and not 0 (stall).
    private float scrapeResumeAirEnterTime = -1f;
    // simTime of the first Plant tick with the Plant→Fall gate shut, −1 while
    // the gate is open. The stall age is a stamp difference on purpose:
    // TickFallCatchWalk has no dt (prevTickTime is only stamped there).
    private float plantGateStallSince = -1f;
    // One re-seat per stall, so a shut gate cannot ping-pong support.
    private bool plantReseated;
    // Last rung of the ladder: Plant writes standLeg 0 (two-foot quiet
    // stance) until the gate opens again.
    private bool plantStandSurrender;
    // Failed catches (Catch→Reach) since the last landing or standing Plant.
    private int fallCatchBounceCount;
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

    // Catch scrape this phase (toe down, heel up). scrapeExtendAirAge
    // starts the air clock only after this, so a true airborne Catch
    // still Max's Last with live Progress.
    private bool catchScrapedThisPhase;
    // Catch-enter Last at that first air frame. scrapeExtendAirAge
    // continues from this, not live Last (Tick would compound).
    private float scrapeResumeExtendStart;

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
