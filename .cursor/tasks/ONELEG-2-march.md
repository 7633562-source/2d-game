# ONELEG-2 — march in place

Walk / WalkLift stay paused. Cold one-leg clock, not `walkActive`.

## Goal

Alternate raised legs in place. Raised pose: thigh toward horizontal
(hip flexion 90°, joint stop −90), shank down (knee ~90°). No forward
travel. No fold.

## Layer

Control: `BalanceController` air hip/knee while `intent.march`.
Intent: `MotionIntent.march`. Experiment: `HeadlessTrial` `-march`
clock writes `standLeg` left / plant / right / plant.

Do not edit WalkLift, look, levels, bird, feet, solver, masses, 200 Hz,
SPD, torque signs. Do not start SIMBICON. Quiet one-leg keep
`swingHipFlex` 40 / `swingKneeFlex` 55.

Skip `standLegPelvisTilt` +10° while marching.

Do not chase CoM with `hipBalanceGain` while marching.

**Pelvis tilt is not the compensator** (bistable, same well as CaptureWalk).

## Wall (2026-09-14, `march_pin`)

The 90° clocked pose holds. CoM over the support does not. The ankle
CoM loop is the same two wells as CaptureWalk: midpoint / late pin
vaults about the toe (+X); stance-only / early pin yanks about the
heel (−X). There is no third reference on this mechanic.

CSV `march_pin`, second lift (right support):

- Dual 9.00–10.170: swing ankle mute, stance `actAnkleR = 1.0` (full
  P=14, cap is air-gated). `comOffset` already −0.09 at 10.00.
- Recovery (`state = 1`) at **10.170** while both feet still down,
  `comOffset` −0.159.
- First left air **10.175**, hip −26°, knee 25°. Pose 90° comes *after*
  the body is already past `recoveryCoMOffset` 0.15 m.
- Heel gate then zeros dorsiflex (`actAnkleR → 0` by 10.300). Dead
  ankle, `comOffset` → −0.30, Falling 10.580. `finalPelvisTilt` +89.7°
  is the fold after `fell`, not the live lean.

First lift 5–7.5 never left the ground (`hipR` ≈ −15°). `marchSwapCount`
counts the clock, not a sole.

Hip-to-hip couple (`walkSwingLiftCouple`) was considered and not
wired: it cancels open-chain swing-hip reaction. This run died in
closed-chain dual peel, before the snap. Another ankle cap / delay /
tilt / extra P is the same well.

Quiet one-leg 40° / 55° still stands. That is a slow ramp, not this
clock. Do not retune PD. Do not start SIMBICON. Resume WALK-1.

| Label | travel | pelvis | survived | knee |
|---|---|---|---|---|
| `march_hold` | **+1.0** | −15° | 18.6 s | 90.4 |
| `march_mid` | **−0.90** | +49° | 13.3 s | 0 |
| `march_soft` | **+0.885** | −17° | 18.9 s | 89.3 |
| `march_ph` | **−0.75** | +89.6° | 10.3 s | 88.6 |
| `march_ank` | **−0.61** | −17° | 19.2 s | **0** |
| `march_air` | **+0.57** | −17° | 10.8 s | **89.7** |
| `march_pin` | **−0.62** | +89.7° | 10.6 s | **86** |

## Protocol (last run)

```
-march 1 -marchTime 5 -marchDuration 24
-marchHold 2.5 -marchPlant 1.5 -marchFirst left
Duration 30
Label march_pin
```

## Acceptance — not met

| Check | Gate | `march_pin` |
|---|---|---|
| `fell` | false, whole run | **true** (10.580 s) |
| `marchSwapCount` | ≥ 4 | 6 (clock) |
| `marchSwingKneeMean` | about 90° | 86 |
| `comTravelX` | < 0.15 m | **−0.62** |
| `pelvisDrop` | no fold | **0.872** |

Quiet stance / `stable-pd` is not this card. Do not open a new baseline.
