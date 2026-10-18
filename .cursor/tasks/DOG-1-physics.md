# DOG-1 — segmental dog, physics

This is physics / body / dog control. One sentence: a sagittal
quadruped stands on four paws with the same actuator split as the
human (segments, hinges, antagonist muscles, passive friction).

Layer: dog body (`Dog`, `HumanSegment`), actuators (`Muscle`,
`JointFriction` — reuse, do not fork), dog sensors (CoM + paw probes +
vestibular on the chest), dog control (`DogStanceController`).
Do not edit human `BalanceController` gains, bird flight, or trees.

## Contract

- Do not rename human public fields. Do not change human torque signs.
- Forces only in `FixedUpdate`. Step 200 Hz (`fixedDelta` 0.005).
- No invisible upward force. No `HingeJoint2D.useMotor`.
- Size is `sprite.size` / `collider.size`, not `localScale`.
- Visual offset on the child `Visual` only (`SetVisualOffset`).
- `jointAngle` sign is the human convention (`balance-actuators.mdc`).
- Paw joints (all four) are load-bearing: no angular PD, no muscle
  damping of paw angle. Support torque against CoM offset only.
- Scene `Dog` for Play (`dogCount = 1`). `SampleScene`, `World`,
  `Bird`, `Tree` keep `dogCount = 0`.
- Headless: `-dog 1`, `humanCount = 0`, flat ground, no birds/trees.
- Do not start a new **human** baseline. Do not delete Runs.
- Do not invent a dog brain / `DogIntent` (no AI card).
- Documents English only.

## Acceptance (stance)

Quiet stance 30 s on the stand:

- `fell: false`
- `survivedSeconds` ≥ 30
- `trunkDrop` (chest or pelvis Y drop) on the order of stance, not a
  fold (human quiet `pelvisDrop` ≈ 0.0017 — a dog fold looks like
  tenths of a metre)
- `fourFeetGroundedFraction` near 1
- `muscleSaturationFraction` (hips/shoulders/knees/elbows only) low
- Human regression: one quiet human stand 10 s after wiring
  `GameProcess` / `HeadlessTrial` must not fold (`fell: false`,
  `pelvisDrop` ≈ 0.0017)

Push (impulse on `Chest` along +X) is diagnostic, not a copied
human threshold. Record hold/fall both ways each cycle.

## Forbids

SIMBICON, 50/100 Hz, mixing dog into `SampleScene` Play, retune
human PD, amputate human arms, `OverlapBoxAll` in `FixedUpdate`.

## Ten cycles

Cycle = apply (or first implement) → stand 30 s + push pair → write
`Trials/Runs/dog_c<N>_*.json` → optimizer note in
`Trials/Runs/dog_opt_inbox.md` → next cycle.
Do not invent a new layer between cycles. Gains, friction K, and
small geometry only. Report a table, not a CSV dump.

## Report

`fell`, `survivedSeconds`, `maxAbsChestPitch`, `maxAbsPelvisTilt`,
`maxAbsLumbarAngle`, `trunkDrop`, `rmsComOffset`,
`fourFeetGroundedFraction`, `muscleSaturationFraction`, push hold/fall.
Log in `Trials/Runs/dog_cycles.md`.
