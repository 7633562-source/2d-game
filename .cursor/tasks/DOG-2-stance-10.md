# DOG-2 — ten stance cycles (sit on spawn)

This is physics. One sentence: the dog must stand on four paws at
spawn instead of folding into a sit.

Yury’s Play shot (02.09 evening): rear on the grass, front paws
stretched +X, chest nose-down, white jaw box, red `DogPrey` ahead.
That is the c52 columnar collapse (`trunkDrop` 0.313, `fourFeet`
0.024…0.065), not a new leap bug. Leap stays inert on these runs.

Layer: `Dog`, `DogStanceController`. Reuse `HumanSegment`, `Muscle`,
`JointFriction`. Do not edit human `BalanceController` gains, bird,
or trees.

## Contract

- Forces only in `FixedUpdate`. No `AddForce` up. No `useMotor`.
- Paw joints: no angular PD. CoM / plant signal only.
- `jointAngle` sign = human convention. Knee/elbow flexion is plus.
- Visual offset only. Do not spread L/R bodies on X.
- Keep jaw, five-vertebra tail, leap machine. Do not invent `DogIntent`.
- Do not touch `SampleScene`. Documents English only.
- Extra CLI numbers as strings with a **dot**.

## What is already measured

| Body | hip/shoulder | `fourFeet` | `trunkDrop` | note |
|---|---|---|---|---|
| **c39 leftover** | −25 / +25 | **0.992** | **0.203** | plant holds; still a sit |
| c41 / c52 columns | 0 / 0 | 0.024 | 0.275…0.313 | rear unloads ~0.15 s, lumbar +20 |
| `dog_leap_reg` | 0 / 0 | 0.0655 | 0.3135 | same sit; leap off |

Columns 0/0 are rejected twice (c41 + re-shot + c52). Do not spend
cycle 1 re-proving that. `fell: false` is not a stand.

Root of 0/0: a vertical column holds a local hip/shoulder angle; the
lumbar reaction spins the pelvis and unloads the rear. Rake −25/+25
puts paws under the belly (support width ~0.24 m) and keeps plant,
but the picture is a sit.

## Ten cycles

Cycle = one hypothesis → one change → stand → table → keep or revert.

Labels `dog_c53` … `dog_c62` (journal is at 52). Append
`Trials/Runs/dog_cycles.md`. Do not delete old tables.

**Cycle 53 (first rebuild):** restore the only known plant as defaults:
`spawnHipAngle` −25, `spawnShoulderAngle` +25, matching
`hipBaseAngle` / `shoulderBaseAngle`. Spawn height from
`StandingRootOffset` (rake shortens `BoneDrop`). Quiet 30 s
`dog_c53_stand`. Expect `fourFeet` near c39 (0.99), `trunkDrop` near
0.20. If jaw + 5-tail + leap code broke plant, isolate that — do not
invent a third pose. One human 10 s (`humanCount` default path, no
`-dog`) after this rebuild: `fell` false, `pelvisDrop` ≈ 0.0017.

**Cycles 54–62:** from the planted body, lower the sit. Goal is
`trunkDrop` on the order of 0.01 and `fourFeet` near 1, chest/pelvis
not 50–70°. One lever per cycle. Legal levers: rake magnitude,
knee/elbow base, `heightP`/`heightD`, `chestTargetTilt`, lumbar
gains/friction, hip/shoulder P, spawn/base match. Illegal: `AddForce`,
paw angle PD, SIMBICON, 50/100 Hz, retune human PD.

Do not return to 0/0 unless a **new** mechanism (not “try columns
again”) is named and the previous cycle still plants.

Coeff-only cycles: no rebuild. Source/default changes: close Unity
(`Tools\close-unity.ps1`) then `-Rebuild`. Check `applied` in the
summary, not intent.

```
powershell -Command ".\Tools\run-trial.ps1 -Duration 30 -Label dog_cNN_stand -Extra @('-dog','1')"
```

Push pair is diagnostic if a cycle actually stands (drop ~0.01).
Do not burn push budget on a sit. Leap off (`-dogLeap` unset).

## Acceptance

Quiet 30 s:

- `fell` false
- `survivedSeconds` ≥ 30
- `trunkDrop` stance-order (~0.01), not tenths of a metre
- `fourFeetGroundedFraction` near 1
- hips/knees/shoulders/elbows saturation low
- human 10 s after first rebuild does not fold

A cycle that keeps plant but still sits (`trunkDrop` ≥ 0.15) is a
step, not done. Do not call c39 “accepted stance”.

## Report

Table per cycle: `fell`, `survivedSeconds`, `maxAbsChestPitch`,
`maxAbsPelvisTilt`, `maxAbsLumbarAngle`, `trunkDrop`, `fourFeet`,
`muscleSaturation`, `applied` hip/shoulder/knee/heightP/chestTilt.
Verdict keep/revert. No CSV dump.

RESULT to parent in Russian, 5–10 lines.
