# WALK-1 · 2D physics — swing lift

This is physics / control. One sentence: the swing must leave the ground
on walk without fold, not by a global toe-off.

Layer: `BalanceController`, `StepPhaseDriver` if needed. Do not touch
body, actuators, sensors, world, or bird.

## Contract

- Do not rename public fields. Do not change torque signs.
- Forces stay in `FixedUpdate`. Step 200 Hz.
- While `standLeg = 0` and walk is off, listed stance/push metrics must
  not be worse than now.
- Do not start SIMBICON. Do not start a new baseline.

## Diagnose first (you may read code; do not retune gains)

On the current walk default (`-walk 1`, duration like `walk_xfer_reg` / 40 s):

- Why `swingFoot` = 1.0: unload/knee is not enough, CoM did not move onto
  stance, swing ankle still holds, cap does not release.
- Where CoM sits vs the stance foot in Stance.
- Why `walk_act_ss3` and peel defaults fold, while `walk_reg_act` stands
  with `swingFoot` 1.0.

Short write-up in `Trials/Runs/walk1_diag.md` plus numbers from existing
summaries if they exist. A new diagnostic run only if old JSON is missing
or the fingerprint differs.

## Then one mechanism

Only after diagnosis, one candidate, not a CLI bundle:

- not a global toe-off on back-swing;
- not a controller substep and not a dt change.

Acceptance run `walk1_lift`:

| Metric | Target |
|---|---|
| duration | 40 s |
| walkSwapCount | ≥ 4 |
| fell | false |
| pelvisDrop | ≤ 0.01 |
| swingFoot | < 0.35 |
| stanceFoot | ≈ 1.0 |
| maxAbsTorsoTilt | watch it; no fall |

Regressions on the same build: stance 30 s, `oneg_l`, pf24, pb23.

## Forbids

Do not retune `lumbarP` / muscles for lift. Do not cut 200 Hz. Do not
merge segments.

## Acceptance

A table of numbers in the reply. Summary `Trials/Runs/walk1_lift.json`
(or an honest blocker: mechanism not found, what was tried, which folds).
No CSV in chat.
