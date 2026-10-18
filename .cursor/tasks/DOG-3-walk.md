# DOG-3 — dog walk ≥ 5 km/h

This is physics. One sentence: the dog walks forward on four paws at
least **5 km/h** (1.389 m/s) and stays a quadruped, not a fold.

Yury: use the paws; give the four limbs **different angles**; try;
the dog must be stable.

Layer: `DogStanceController` (+ recorder / CLI). Reuse `Muscle`,
`JointFriction`. Do not edit human `BalanceController` walk, bird
`walkThrust`, or trees.

## Contract

- No `AddForce` / `AddTorque` for locomotion. No `useMotor`.
- Paw joints: **no angular PD**. Plant / CoM muscle signal only.
- Do not invent `DogIntent`. Walk is `Walk()` from `DogDrive` or CLI
  `-dogWalk`. No keyboard. The dog is AI, not the player.
- Do not spread L/R bodies on X. Front vs rear stay body length.
- Do not touch `SampleScene`. Do not retune human PD.
- Other chats own human walk CLI and the mixed `GameProcess` yard.
  Add dog walk lines; do not revert their fields.
- Extra numbers as strings with a **dot**.
- Documents English only.

## Body you start from

DOG-2 leftover (baked): rake hip **−25** / shoulder **+25**,
`startupHipP` 8 / 0.25 s. Quiet sit: `trunkDrop` **0.193**,
`fourFeet` **0.992**, `fell` false. Columns 0/0 stay rejected.
Leap stays; walk off on stance runs.

Human walk on this project is a warning, not a template: the
quasi-static support swap travels ~6 mm/s. Do not copy
`StepPhaseDriver` 8 s stance. A dog walk needs a **clocked**
cadence and a real plant push.

## Gait (required)

Sagittal trot or walk with **four independent joint targets**.
Do not send one hip/shoulder/knee to all four limbs.

Suggested first cut — diagonal trot (even though L/R share X,
the two columns of a pair must not share one pose):

| Phase | Front-right + rear-left | Front-left + rear-right |
|---|---|---|
| A | stance: hip/shoulder toward extension, paw plant + | swing: hip more minus / shoulder more plus, knee/elbow plus |
| B | swing | stance |

Period first guess **0.35–0.50 s** (full A+B). Speed ≈ cadence ×
reach. 5 km/h needs about **1.4 m/s**; a 0.30 m reach at 4.5 Hz
pair-rate is the right order. If the sit cannot reach, say so
with numbers — do not fake speed with `AddForce`.

Stance paws: `leapPawPlant`-style extensor while grounded.
Swing paws: zero or light CoM only after lift. Knee/elbow plus
on swing, toward 0 on stance (digitigrade, not a sit fold).

`Sense` must not `RelaxAll` just because CoM left the mid-paws
during a step. Fold only on real trunk drop (same idea as leap
`EndLeap`).

## Metrics

Add to `DogTrialRecorder` / summary (new keys, do not rename
human ones):

- `comTravelX`, `comSpeedX` (m/s), `comSpeedKmh` (×3.6)
- `walkCycleCount`, `walkActiveFraction`
- existing `fell`, `trunkDrop`, `fourFeet`, saturation, pitches

5 km/h means **`comSpeedX` ≥ 1.389** on the walk window (after
the first second), not a fall that slides 20 m.

## Acceptance

Walk 15–20 s, `-dogWalk 1 -dogWalkTime 1`:

- `fell` false, `survivedSeconds` ≥ requested
- `comSpeedX` ≥ **1.389** m/s (5 km/h) in the walk window
- `trunkDrop` not a belly-slide (tenths of a metre while
  `fourFeet` → 0 and chest 50°+ is a fold, not a gait)
- limbs actually cycle (CSV / `walkCycleCount` ≥ 1)
- quiet `-dog 1` 10 s with walk off still plants (`fourFeet`
  near leftover, no worse than noise)

Play: `DogDrive` walks. No keys. Headless `-dogWalk` still calls `Walk()`.

## Cycles

Implement gait → stand → table → one lever → again.
Labels `dog_w1` … until the speed gate or a hard stop.
Append `Trials/Runs/dog_cycles.md`. Close Unity before `-Rebuild`.

```
powershell -Command ".\Tools\run-trial.ps1 -Rebuild -Duration 20 -Label dog_w1 -Extra @('-dog','1','-dogWalk','1','-dogWalkTime','1')"
```

## Forbids

SIMBICON, 50/100 Hz, paw pose PD, invisible up-force, `DogIntent`,
rewriting human walk, deleting Runs.

## Report

Table: speed m/s and km/h, travel, drop, fourFeet, fell, cycles.
Honest if 5 km/h is not reached. RESULT to parent in Russian.
