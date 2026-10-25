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

    // Failed catches (Catch→Reach) since the last landing or standing Plant.
    private int fallCatchBounceCount;

    // Catch scrape this phase (toe down, heel up). scrapeExtendAirAge
    // starts the air clock only after this, so a true airborne Catch
    // still Max's Last with live Progress.
    private bool catchScrapedThisPhase;
    // Catch-enter Last at that first air frame. scrapeExtendAirAge
    // continues from this, not live Last (Tick would compound).
    private float scrapeResumeExtendStart;
}
