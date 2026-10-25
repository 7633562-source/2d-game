using UnityEngine;

// Intent layer: one-leg stance on a timer, transfer — by condition (both feet,
// CoM at center). Do not touch BalanceController.
[DefaultExecutionOrder(-50)]
public partial class StepPhaseDriver : MonoBehaviour
{
    // WALK-2 limit-cycle walker. Off = legacy Stance/Transfer only.
    public enum FallCatchPhase
    {
        Plant = 0,
        Fall = 1,
        Reach = 2,
        Catch = 3,
        Recover = 4
    }

    private enum Phase
    {
        Stance,
        Transfer
    }

    [Tooltip("First phase: +1 right stance, −1 left.")]
    public float firstStance = 1f;

    [Header("Fall–catch walk (WALK-2)")]
    // Play/WebGL default 1: W starts this gait. HeadlessTrial CLI default is
    // 0 so a quiet -walk 1 stays the legacy Stance/Transfer path.
    [Tooltip("1 = Plant→Fall→Reach→Catch→Recover FSM. 0 = legacy Stance/Transfer only.")]
    public int useFallCatchWalk = 1;
}
