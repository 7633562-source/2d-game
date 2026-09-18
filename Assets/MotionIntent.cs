using UnityEngine;

// Intent layer: what the human wants, not how the body gets it.
// The component stores numbers only and has no logic. Anyone may fill them:
// PlayerInputSource from the keyboard, the windowless stand in code,
// later — a creature AI.
//
// Input is deliberately not read by the balancer. First, the windowless
// stand has no keyboard, and the run would need conditional compilation.
// Second, with a hundred creatures only one — the one being played —
// should poll the keyboard, not each in its own FixedUpdate.
//
// This holds the target, not a smoothed value: keyboard crouch is only
// 0 or 1. The control layer does the pose blend; otherwise the stand
// that sets crouch = 1 directly would get an instant squat and behave
// differently from the same key press.
public class MotionIntent : MonoBehaviour
{
    [Header("Move direction: -1 back, +1 forward, 0 stand")]
    [Range(-1f, 1f)]
    public float moveX;

    [Header("Crouch target: 0 standing, 1 squatting")]
    [Range(0f, 1f)]
    public float crouch;

    // Which leg carries the weight. 0 — both (former symmetry), −1 — stance
    // on the left (swing on the right), +1 — stance on the right (swing on
    // the left). This is intent, not a smoothed level: the ramp-up is done
    // by BalanceController, otherwise the stand with an instant standLeg
    // would outrun a held key.
    [Header("Support: 0 both feet, -1 left, +1 right")]
    [Range(-1f, 1f)]
    public float standLeg;

    // March in place: the stand writes standLeg left/right on a clock.
    // The balancer reads this so the 90° swing pose and the skipped
    // pelvis tilt do not leak into a quiet one-leg hold. Not a walk.
    [Header("March in place: 0 off, 1 on")]
    [Range(0f, 1f)]
    public float march;

    // Torso lean while on two feet: +1 forward (+X), −1 backward.
    // Not to be confused with moveX (walking): ←/A and →/D write lean.
    // Walk is W / ↑ / Space or the on-screen Walk button.
    [Header("Torso lean: -1 back, +1 forward, 0 vertical")]
    [Range(-1f, 1f)]
    public float lean;

    // Run: 1 while walk is held together with Shift. Shorter cadence than walk.
    [Header("Run: 0 walk/stand, 1 run")]
    [Range(0f, 1f)]
    public float run;
}
