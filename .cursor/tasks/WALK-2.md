# WALK-2 — Dynamic fall–catch limit-cycle walker

Design 2026-09-06 (physics, read-only). Replaces the exhausted quasi-static
walk (r26–r31, walk1, walk1_vault04: travel → fold ~6 s, no travel → static).

## Root cause
`BalanceController` holds CoM over the support every frame (ankle PD +
world-upright pelvis/lumbar). This cancels the momentum a dynamic step needs.
Any forward impulse folds the model; without it, swingFoot ≈ 1.0.

## Mechanism
Explicit 5-phase FSM in `StepPhaseDriver`, master switch `useFallCatchWalk`
(default 0 = current behavior). In Fall the ankle balance is deliberately
weakened and pelvis/torso lean forward; the swing leg catches in Catch.

Phases: Plant → Fall → Reach → Catch → Recover → Plant.

New fields: `fallComLeadTrigger`, `fallPelvisLean`, `catchKneeFlex`,
`plantMinDuration`, `reachTimeout`, `fallAbortDeg`.

## Gait initiation (Yury's observation — hypothesis, not a decision)

Proposal from visual review: a step should begin with knee lift (swing-knee
flexes, foot up) + stance drives forward + forward lean, NOT a backward leg
swing and NOT a push-off. The model currently swings the swing leg backward
first and topples. VALIDATE with the physics specialist before implementing.

## Staged acceptance
1. walk2_debug — FSM + phase logging, no balance change: phase transitions in CSV.
2. walk2_cycle — fell=false, pelvisDrop≤0.01, fallCatchCycleCount≥4, maxAbsTorsoTilt≤20°.
3. walk2_lift — swingFoot<0.35, stanceFoot≈1.0, fallCatchCycleCount≥4.
4. walk2_travel — comTravelX≥0.35 m (best now 0.21), comSpeedX≥0.008, fell=false.
5. Regressions: stance RMS≤0.0055, oneg swingFoot≈0.30, pf23/pb23 per current-front.

## Forbids
No SIMBICON, no ankle angle PD, no AddForce, no segment merge, keep 200 Hz,
do not retune lumbarP/muscles for lift. WALK-1 (weightBlend/vault) stays
frozen; `useFallCatchWalk=0` must fully bypass the new code.
