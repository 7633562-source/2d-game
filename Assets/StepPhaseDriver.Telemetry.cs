using UnityEngine;

public partial class StepPhaseDriver
{
    // Headless BeginWalk writes the simulated clock. Capture-walk
    // onset logging (first 3 s) measures age from this, not from a
    // physics-step counter that ResetCaptureWalk zeroes.
    public float WalkStartTime => walkStartTime;
    // Counters for the summary: without them the CSV looks "ready" but the swap is invisible.
    public int SwapCount { get; private set; }
    public int TransferReadyFrames { get; private set; }
    public int FallCatchCycleCount { get; private set; }

    // Failed Fall/Reach abort: Plant holds the same stance and the Recover
    // brake until TiltSettled + signed CoM lead, then the next Fall may start.
    // Not a completed cycle — FallCatchCycleCount stays put.
    public bool StandingRestore { get; private set; }
    // Recovery ladder is holding two-foot quiet stance instead of a split
    // Plant. Not a phase and not a cycle — the walk resumes from the same
    // Plant once the gate opens.
    public bool StandingSurrender => plantStandSurrender;

    public bool SpawnStandHeightValid => spawnStandHeightValid;
    public float SpawnStandHeight => spawnStandHeight;
    public bool StandComHeightValid => standComHeightValid;
    public float StandComHeight => standComHeight;
    public bool HasStandingComY => standComHeightValid || spawnStandHeightValid;
    public float StandingComY => standComHeightValid ? standComHeight : spawnStandHeight;

    public int FallCatchPhaseCode =>
        useFallCatchWalk != 0 && walkActive ? (int)fallCatchPhase : -1;
    public int PhaseCode
    {
        get
        {
            // Capture walker owns the gait: Tick returns without writing
            // fallCatchPhase or the legacy Stance/Transfer clock, so those
            // stay at Plant / Stance = 0 for the whole run. Report the
            // walker's own state so CSV walkPhase and the overlay move.
            if (balance == null)
                EnsureRefs();
            if (balance != null && balance.useCaptureWalker != 0 && walkActive)
                return balance.CaptureWalkStateCode;
            // WalkLift never EnterTransfer (that DualSupport is the dump).
            // Overlay Unload/Clear/Advance is the step; report it so
            // CSV walkPhase leaves Stance=0 once the swing knee lifts.
            if (balance != null && balance.useWalkLift != 0 && walkActive
                && balance.WalkLiftPhaseCode != 0)
                return balance.WalkLiftPhaseCode;
            return useFallCatchWalk != 0
                ? (fallCatchPhase <= FallCatchPhase.Reach ? 0 : 1)
                : (phase == Phase.Transfer ? 1 : 0);
        }
    }

    // Age on the clock of the last Tick (stand — simulated, Play — fixedTime).
    // Balance used to take Time.fixedTime on Tick(sim) — different axes.
    public float PhaseAge() => lastTickTime - phaseEnterTime;

    // Plant is standing on two feet on purpose, waiting for the CoM to roll
    // over the stance foot. Read-only, for the Play overlay.
    public bool PlantWaitingForLead { get; private set; }
}
