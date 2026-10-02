# PERF-1 · 2D physics

This is physics. One sentence: need the cost of one Human at 200 Hz and legal model forks, without editing the controller.

Layer: body / actuators / sensors (read). Do not change `BalanceController`, torque signs, PD/SPD. Do not touch look, levels, or the bird.

## Contract

- Do not rename human or stand public fields.
- Balance acceptance is still `Trials/Baselines/stable-pd/` as the historical anchor; fresh numbers come from the current run, do not replace the baseline.
- Folder `Trials/Baselines/current/` is the outdated ostrich knee, not the target.
- If the design contradicts `balance-actuators.mdc` — return the contradiction, do not silently fix it.

## Do

1. Cost inventory: count of `Rigidbody2D`, joints, `Muscle`, `JointFriction`, CCD bodies, FixedUpdate passes, typical contacts/probes per step.
2. Lean on already captured: `wallPerSim` 0.77–0.87, CCD=2, Muscle=30, Friction=15, solver 8/3. Do not repeat the five margin cycles.
3. Table: what can still get cheaper without a concept change (cache, NonAlloc, leftover Find — only if it is in your layer and does not break two-foot CSV).
4. Concept forks if the margin is exhausted for the game “one honest ragdoll”:
   - A keep 200 Hz, cut only contacts/work outside the model;
   - B dual-rate loop (coarser physics / more frequent controller) — cost and why the 50 Hz stand is already closed;
   - C simplify the body (merge segments, drop arms) — why this breaks CoM and stance;
   - D another actuator/solver.
   Do not start implementation.

## Forbids

Do not cut 200 Hz. Do not tune PD. Do not merge pelvis with chest. Do not amputate arms. Do not bring back `HingeJoint2D.useMotor`. Do not invent a new layer.

## Acceptance

`Trials/Runs/perf_wave1_bodycost.json`: inventory + 4 forks (cost, risk, whether it breaks listed metrics). Table of numbers, no CSV.
