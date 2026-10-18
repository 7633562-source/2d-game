# ONELEG-1 — one-leg hold quality (vestibular)

Walk / WalkLift stay paused. Cold one-leg only.

## Goal

Hold one-leg stance on both supports. Measure pelvis rock and CoM drift
in the settled hold window (swing airborne + 2 s). Tighten the band.
Extend hold to 20 s.

## Layer

Control: `BalanceController` (cold-one-leg ankle mute; extra pelvis P/D
default 0 and single-support only). Sensors stay read-only.
Experiment: `TrialRecorder` hold metrics, `HeadlessTrial` CLI.

Do not edit WalkLift, look, levels, bird, feet, solver, masses, 200 Hz.
Do not start SIMBICON. Do not retune quiet-stance `pelvisP` / `pelvisD`.

Left vs right: left support still uses `standLegPelvisTilt` +10° (forward
swing). Right support has no extra tilt (back swing). That mapping stays.
Do not add the +10° on right — it plants both feet.

## Protocol

```
-standLeg right|left -standLegTime 5 -standLegHold 20
Duration 30
Label oneleg_perf / oneleg_perf_l
```

## Acceptance

| Check | Gate |
|---|---|
| `fell` | false, both legs |
| hold | 20 s (`standLegHold`) |
| `oneLegPelvisSpan` | < 3° (settled window) |
| `oneLegComDriftX` | < 0.10 m |

`maxAbsPelvisTilt` includes lift; do not use it as the rock gate.
Quiet stance / push / walk flags are not this card.

CLI: `-oneLegPelvisP` / `-oneLegPelvisD` (defaults **0 / 0**).
oneleg_perf_l: extra 1/2 dumped left support. Do not put them back
on the closed-chain lift. March-in-place is the next card, not this one.
