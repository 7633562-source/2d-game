using UnityEngine;

public partial class StepPhaseDriver
{
    // Commanded stance in the air, the other foot down: stand on the
    // loaded one. Both down or both up: keep the identity (geometric
    // lead would swap after a missed catch).
    private void HoldLoadedStance()
    {
        if (bodyState == null || IsStanceFootGrounded())
            return;
        bool leftDown = FootDown(true);
        bool rightDown = FootDown(false);
        if (leftDown == rightDown)
            return;
        currentStance = leftDown ? -1f : 1f;
    }

    // Plant ladder rung 1. CoM has run past the commanded support while the
    // other foot is already planted further along the walk: standing on that
    // foot puts the lead back inside the band at once. Not a step — no
    // SwapCount, no FallCatchCycleCount, and the margin keeps a healthy
    // negative-lead Plant (CoM behind its own support) untouched.
    private bool TryReseatStanceToLoadedFoot()
    {
        if (bodyState == null || comCalculator == null)
            return false;
        if (!BothFeetGrounded())
            return false;
        float otherLead = OtherFootComLead();
        if (otherLead >= StanceComLead() - Mathf.Max(0f, plantReseatLeadMargin))
            return false;
        currentStance = -currentStance;
        return true;
    }

    // Signed lead of the current swing foot, same convention as StanceComLead.
    private float OtherFootComLead()
    {
        return SwingComLead();
    }

    // Reference for the sink guard. Only a settled two-foot pose that
    // passed the Plant→Fall gate counts as "this is how tall standing
    // is here". World CoM from the sensors after settle — never the
    // pelvis spawn column (~0.28 m too high → sinkAbortDrop on quiet
    // stance) and never Max of a walk-orbit peak (that held the bar
    // 0.25 m above live CoM and locked DualSupport as collapsed).
    private void CaptureStandHeight()
    {
        if (comCalculator == null || !BothFeetGrounded())
            return;
        if (comCalculator.TrackedBodyCount <= 0)
            return;
        if (!PlantSensorsReady())
            return;
        if (AbsBodyTilt() >= TiltSettledMaxDeg)
            return;
        if (!BothFeetPlanted())
            return;
        float comY = comCalculator.GetCenterOfMass().y;
        // Settled world sensors only. Never Max of a walk-orbit peak
        // (that held the bar ~0.25 m above live CoM → DualSupport
        // collapsed ~99 % of onset and GETUP BodyCollapsed forever).
        // Geometric pelvis column is RetargetStandComIfColumn only.
        RetargetStandComIfColumn(comY);
    }

    // Folding is not tipping: tilt and CoM offset both stay calm while the
    // body sinks (open defect 8). Inert until a stand has been measured, or
    // until walkRecoverAnyPose has the spawn CoM to compare against.
    // A requested crouch is the same height drop on purpose: only a heap
    // (tilt past the crouch lean) or a crash (CoM still falling fast)
    // is an abort then. Uncommanded drop is still a fold.

    // Read-only for BalanceController. The catch exemption from the fall
    // automaton must not outlive the height a catch needs: a body already
    // on the slab has no stance left to reach from.
    public bool BodyCollapsed => SinkCollapsed();

    private bool SinkCollapsed()
    {
        if (sinkAbortDrop <= 0.001f || comCalculator == null)
            return false;
        // GETUP-1 / Fallen: the heap is below any standing bar by
        // definition. Dog already clears WalkCollapsed here; human
        // BodyCollapsed must not freeze TickGetUp for 32 s.
        if (balance == null)
            EnsureRefs();
        if (balance != null
            && (balance.locomotionState
                    == BalanceController.LocomotionState.GettingUp
                || balance.locomotionState
                    == BalanceController.LocomotionState.Fallen))
            return false;
        float comY = comCalculator.GetCenterOfMass().y;
        float reference;
        if (standComHeightValid)
            reference = standComHeight;
        else if (spawnStandHeightValid && walkRecoverAnyPose > 0.5f)
            reference = spawnStandHeight;
        else
            return false;

        // Quiet stance is not a 0.28 m fold. The pelvis column / root
        // fallback sits ~0.28 m above live CoM, so drop > sinkAbortDrop
        // 0.25 while pelvisDrop is 0.0017 and StandingRestore kills the
        // capture walker. A real heap drops the pelvis ~0.8 m (defect 8).
        // Geometric column is only this pelvis test, never the CoM bar.
        if (PelvisNearSpawnStand()
            && PlantSensorsReady()
            && AbsBodyTilt() < TiltSettledMaxDeg
            && BothFeetPlanted())
        {
            if (reference - comY >= sinkAbortDrop)
                RetargetStandComIfColumn(comY);
            return false;
        }

        if (reference - comY < sinkAbortDrop)
            return false;
        if (intent != null && intent.crouch > 0.5f)
        {
            float tilt = AbsBodyTilt();
            float vy = bodyState != null ? bodyState.comVelocity.y : 0f;
            return tilt >= sinkCollapseTiltDeg || vy <= sinkCollapseVy;
        }
        return true;
    }

    private void RetargetStandComIfColumn(float comY)
    {
        spawnStandHeight = comY;
        spawnStandHeightValid = true;
        standComHeight = comY;
        standComHeightValid = true;
    }

    private bool PelvisNearSpawnStand()
    {
        if (pelvisTf == null)
            return false;
        float spawnY = spawnPelvisYValid ? spawnPelvisY : GeometricPelvisStandY();
        return pelvisTf.position.y > spawnY - Mathf.Max(0.05f, pelvisStandSlack);
    }

    // Root spawn y, same column as GameProcess. Not a CoM bar.
    private float GeometricPelvisStandY()
    {
        if (humanBody == null)
            return -0.82f;
        float column = (humanBody.pelvisSize.y + humanBody.torsoSize.y) * 0.5f
            + humanBody.thighSize.y + humanBody.shinSize.y + humanBody.footSize.y;
        return -2f + column;
    }

    // Spawn CoM, once, after the body settles. Called from Start, from
    // FixedUpdate while idle, and from BeginWalk before walkActive.
    // World Y from CenterOfMassCalculator (sensors), not the pelvis
    // column and not a startup bounce. TrackedBodyCount > 0 means
    // BuildHuman filled the list — otherwise GetCenterOfMass returns
    // the root. Find is cached.
    private void TryCaptureSpawnStandHeight()
    {
        if (spawnStandHeightValid || walkRecoverAnyPose <= 0.5f)
            return;
        if (walkActive)
            return;
        EnsureRefs();
        if (pelvisTf == null || comCalculator == null)
            return;
        if (comCalculator.TrackedBodyCount <= 0)
            return;
        // sensorTick 0: FootDown still treats clearance 0 as down, so a
        // BeginWalk in Update would stamp the spawn bar from zeros.
        // Idle FixedUpdate is after BodyStateEstimator (−100).
        if (bodyState == null || bodyState.sensorTick <= 0)
            return;
        if (!BothFeetGrounded() || !BothFeetPlanted())
            return;
        if (!PlantSensorsReady())
            return;
        // Settled trunk only. A mixer spike or spawn drop must not
        // write the standing bar.
        if (AbsBodyTilt() >= TiltSettledMaxDeg)
            return;
        // World-space settle: wait out the spawn bounce. Capturing mid-
        // slam arms a column ~0.25 m above quiet CoM → DualSupport
        // collapsed ~99 % of onset (reason 2) and GETUP never clears
        // GetUpComAtStandHeight. Same band as dog settle (0.12 / 0.20).
        if (Mathf.Abs(bodyState.comVelocity.y) > 0.12f)
            return;
        if (Mathf.Abs(bodyState.comVelocity.x) > 0.20f)
            return;
        spawnStandHeight = comCalculator.GetCenterOfMass().y;
        spawnStandHeightValid = true;
        spawnPelvisY = pelvisTf.position.y;
        spawnPelvisYValid = true;
        RetargetStandComIfColumn(spawnStandHeight);
    }

    // GETUP-1: drop any walk-orbit peak so GetUpComAtStandHeight can
    // pass once the trunk is upright over the feet. spawnPelvisY stays
    // for PelvisNearSpawnStand. NotifyStoodUp / CaptureStandHeight
    // remeasure from the risen pose.
    public void ClearStandingComBarForGetUp()
    {
        standComHeightValid = false;
        standComHeight = 0f;
        spawnStandHeightValid = false;
        spawnStandHeight = 0f;
    }
}
