using UnityEngine;

public partial class StepPhaseDriver
{
    // A new fall may only start from a body that still stands over its foot.
    // At cycle-1 Plant CoM was already 0.38 m past support (fallCoMOffset is
    // 0.30) and Fall added forward CoP on top: the stance leg then folded
    // (hipR → +90°, pelvisY −1.02 → −1.84). Standing and braking beats that.
    [Tooltip("Max CoM lead over the stance foot (m) that still allows a new Fall. Past this Plant keeps the landing brake instead. Signed: CoM behind the foot is always allowed — that is where a catch leaves the body.")]
    public float plantComLeadMax = 0.10f;

    // Tilt cannot see a fold: the body sinks with CoM still over the feet
    // and the trunk inside the abort band (open defect 8 — fell:false at
    // pelvisDrop 0.84). Height is the quantity that moves. The reference is
    // the measured standing CoM at spawn, then a pose that passed the gate
    // on two feet. The pelvis spawn column is ~0.28 m above live CoM (leg
    // mass hangs under the pelvis) and must not arm this guard. Before a
    // stand has been measured the guard stays inert instead of guessing.
    [Tooltip("CoM drop (m) below the last validated standing height that counts as a collapse: any phase returns to a two-foot standing Plant. 0 = off.")]
    public float sinkAbortDrop = 0.25f;

    [Tooltip("Pelvis sag (m) still counted as standing when judging the CoM bar. Quiet sag is 0.0017; a fold is ~0.84. Not a CoM reference.")]
    public float pelvisStandSlack = 0.12f;

    // Deep crouch (crouchKneeFlex 100) drops the pelvis ~0.42 m — past
    // sinkAbortDrop — with CoM still over the feet and the trunk inside
    // crouchTorsoLean (18°). Height alone then fires the emergency Plant
    // as if the body had folded (defect 8). Collapse through the same
    // height is either still falling or already a heap; a held squat is
    // neither. Uncommanded drop keeps the original height-only abort.
    [Tooltip("Downward CoM speed (m/s) that, during a requested crouch, still counts as collapsing through sinkAbortDrop. A held / slowly lowering squat stays above this.")]
    public float sinkCollapseVy = -1.5f;

    [Tooltip("Torso tilt (deg) past which a low CoM during crouch is a heap, not a squat. Above crouchTorsoLean (18), below fallAbortDeg (30).")]
    public float sinkCollapseTiltDeg = 25f;

    // Cold BeginWalk with both feet off the ground: geometric lead can hand
    // stance to the trailing foot, and the brake then holds a leg that is
    // not going to carry anything. The lower foot lands first.
    [Tooltip("Both feet airborne: pick the lower foot as stance instead of the geometric lead. 0 = former lead-only pick.")]
    public float plantAirLowestFoot = 1f;
}
