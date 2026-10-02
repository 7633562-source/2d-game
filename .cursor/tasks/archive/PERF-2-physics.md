# PERF-2 · 2D physics — 100 Hz step

This is physics. One sentence: measure what a slower FixedUpdate (100 Hz) gives, **without** moving torques into Update.

Layer: experiment / actuators / control — CLI `-fixedDelta` only. Do not change controller or muscle code.

## Contract

- Forces stay in FixedUpdate. `Update` + `deltaTime` for `AddTorque` is forbidden.
- Do not rename public fields.
- One parameter: `-fixedDelta 0.01` vs default `0.005`.
- No other Extra. No Rebuild if the build is fresh.

## Do

1. Stance 30 s: label `ts200_stand` (default) and `ts100_stand` (`-fixedDelta 0.01`).
2. Forward push 23 N·s: `ts200_pf23`, `ts100_pf23`.
3. Into the table: fell, survivedSeconds, maxAbsTorsoTilt, pelvisDrop, rmsComOffset, muscleSaturationFraction, wallPerSim, fixedDeltaTime from the summary.
4. Briefly: why SPD/JointFriction are tied to `Time.fixedDeltaTime` and what a variable dt from Update will break.
5. Verdict: 100 Hz better / worse / noise; recommend A or B. Do not propose C for implementation.

## Forbids

Do not edit `BalanceController`, `Muscle`, `JointFriction`. Do not go below 100 Hz. Do not enable manual `Physics2D.Simulate`.

## Acceptance

`Trials/Runs/perf_wave2_timestep.json` + a table in the reply. No CSV in chat.
