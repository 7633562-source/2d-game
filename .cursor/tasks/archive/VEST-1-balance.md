# VEST-1 — ACCEPTED 15.09

Quiet vestibular hold on both supports and both 23 N·s pushes.
March and WalkLift stayed paused. No SIMBICON. No new baseline.

Yury 15.09: full regress green. Next card is `VEST-2-balance.md`.

## Accepted runs

| Run | Gate | Result |
|---|---|---|
| `oneleg_sym_l5` left 30 s | `fell` false, span ~0.79° | `fell` false, 30.00 s, `pelvisDrop` 0.025 |
| `oneleg_sym_r5` right 30 s | `fell` false, span ~0.75° | `fell` false, 30.00 s, `pelvisDrop` 0.033 |
| `pf23r` +23 N·s | holds | `fell` false, `maxComOffsetAfterPush` 0.116, recovery 2.81 s |
| `pb23r` −23 N·s | holds | `fell` false, `maxComOffsetAfterPush` 0.075, recovery 1.38 s |

Quiet freeze of `standLegLevel` until swing plant (left return) did not
break right, forward, or backward. `hipBalanceGain` 8 stays. Quiet
`ProtectJointLimit` margin 8°.

`oneleg_sym_l4` was the left return dump at 27.06 s (level hit 0 with
swing still air). Closed by the freeze, not by PD.

Original card body follows.

---

# VEST-1 — one vestibular balance, not a lift

March and WalkLift stay paused. Quiet stance / quiet one-leg / push
only. Do not start SIMBICON.

## Goal

The human holds CoM over the current support and holds the trunk up
in every quiet situation: dual stance, one-leg hold, external push.
A raised leg is a consequence of CoM already on the other foot, not
a separate controller.

## Layer

Control: `BalanceController` (ankle CoM, pelvis world, lumbar world).
Sensors stay read-only.

Do not edit WalkLift, CaptureWalk, march, look, levels, bird, feet,
solver, masses, 200 Hz, SPD, torque signs.

## Levers that held

- Do not zero `hipBalanceGain` on quiet (`oneleg_sym3` dump at 2.05 s).
- Quiet `ProtectJointLimit` margin **8°** (zeros at +37°). Walk/catch 15°.
- Quiet release: freeze `standLegLevel` until the swing plants (1 s cap),
  then 4/s. Left peel `standLegPelvisTilt` +10° unchanged.
