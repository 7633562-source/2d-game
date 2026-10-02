# VEST-2 — ankle first, one CoM loop

VEST-1 is accepted. Quiet hold and 23 N·s both ways are green.
This card is the next vestibular step, not WalkLift and not a catch
step. Do not start SIMBICON.

Yury: a real human uses a visible strategy — ankle, then pelvis, then
a catch step. The current push damps by micro-shifting the plates.
That is not strategy 1. Do not add a march clock to hide it.

## Chain (later cards do not start here)

| # | Strategy | When | Not this card |
|---|---|---|---|
| 1 | Ankle — CoP inside the plates, **both feet stay down** | CoM still in the base | **this card** |
| 2 | Hip — visible pelvis / hip shear | ankle at the geometric ceiling (toe/heel) | later |
| 3 | Step — one plate unloads because CoM/xi left the base | hold cannot | later; lift and march fall out of this, they are not a second controller |

Do not skip to 2 or 3 while both quiet ankles still copy the same
midpoint command. A catch step now would bake the shuffle into a gait.

## Why the feet shuffle (measured)

Quiet dual writes **one** `ankleBalance` from midpoint `comOffset` and
sends it to **both** plates (`BalanceController`, `!splitLegs`).

The walk comments already know the geometry: plantarflex at a foot the
CoM has passed is push-off; the same torque at a foot still ahead of
CoM restores. Quiet dual does not use that gate.

`pf23r` (+23 N·s, Torso +X) vs `pb23r` (−23):

| | `pf23r` forward | `pb23r` backward |
|---|---|---|
| `fell` | false | false |
| `maxComOffsetAfterPush` | 0.116 | 0.075 |
| `recoverySeconds` | 2.81 | 1.38 |
| `bothFeetGroundedFraction` | **0.9750** | **0.9987** |
| `comTravelX` | **0.465 m** | 0.070 m |
| `recoveryFraction` | 0 (never hit 0.15 m) | 0 |

Forward: CoM rides toward the toes. The **rear** plate still gets the
same “CoM ahead → plantarflex” as the lead. That unloads the rear
foot, the support midpoint jumps, the plate plants again — micro-steps
while `BalanceState` stays Balancing. Backward barely lifts. Hip chase
(`hipBalanceGain` 8) stays; VEST-1 showed zeroing it dumps quiet
stance at 2.05 s. Do not drop it here.

## Step 1 (this card) — per-foot quiet ankle

Layer: `BalanceController`, quiet path only (`QuietVestibularPath`).
Each grounded ankle’s CoM command is about **that plate**, not a copy
of the midpoint signal.

**Coded 15.09.** Quiet dual (`QuietVestibularPath`, both down, no
split): midpoint `ankleBalance` stays the pendulum error. Plantarflex
(plus) is zeroed on a plate whose toes CoM has passed
(`quietAnklePerFoot` 1, default on). Walk / march / CaptureWalk /
one-leg split are untouched. `hipBalanceGain` 8 stays.

- Foot still ahead of CoM: restoring plantarflex (strategy 1).
- Foot the CoM has passed: do **not** plantarflex (that is push-off /
  an accidental step). Hold or mute that ankle.
- One-leg already uses `stanceComOff` — do not restripe it.
- Walk / march / CaptureWalk / FallCatch: do not edit.
- `hipBalanceGain` 8 stays. Do not retune `ankleComP` / `pelvisP` /
  `lumbarP` unless a listed run forces it.
- Torque signs stay. 200 Hz stays. No new baseline.

A raised leg later is the same test: when CoM sits on one plate, the
other ankle is already quiet, so lift is a consequence, not a clock.

Contract: public field names stay. One new quiet-only field is allowed
if the default keeps VEST-1 bitwise until the flag is on; the listed
runs must run with the new behaviour on.

## Do not

- A Recovery/Falling step, WalkLift, march pose, SIMBICON, CaptureWalk.
- Zero `hipBalanceGain`. Toe/heel march gates. Ankle angle PD.
- “Both feet 1.0 by welding the ankles.” The plates must stay down
  because CoP moved inside them, not because the feet cannot leave.

## Acceptance

Same args as the VEST-1 regress. Compare to `pf23r` / `pb23r` /
`oneleg_sym_l5` / `_r5` and `Trials/Baselines/stable-pd/`.

| Run | Gate |
|---|---|
| quiet 30 s | `fell` false, `pelvisDrop` ~0.0017, RMS ~0.005, sat 0, **both feet 1.0000** |
| `oneleg_sym_l5` / `_r5` args, new labels | `fell` false, 30 s, span still ~0.79° / 0.75°, drop not a fold |
| forward 23 N·s (`pf23r` args) | holds; `maxComOffsetAfterPush` not worse than ~0.12; **`bothFeetGroundedFraction` ≥ 0.995** (was 0.975); **`comTravelX` well below 0.46 m** — the shuffle is the bug, not the hold |
| backward 23 N·s | holds; offset ~0.075; feet already ~0.999, do not worsen |

Noise: tenths of a degree are not a result. `recoveryFraction` may stay
0 (offset 0.116 < 0.15). That is fine: this card is still hold, not a
step. Hip strategy and a catch step are later cards.

## First run (after the edit)

```
-Rebuild -Duration 15 -Label pf23v2 -Extra @('-pushImpulse','23','-pushTime','5')
```

If feet still lift, do not retune P. Read which ankle plantarflexes
after the push (rear vs lead). Then the same four VEST-1 regress labels.
