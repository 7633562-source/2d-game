# PERF-2 — timestep: Update/deltaTime vs FixedUpdate

Date: 2026-08-29. Dispatcher: TEAM LEAD.
Reason: “work with Update and deltaTime, without FixedUpdate, it runs too often”.

## Dispatcher verdict (before numbers)

**Moving forces and torques into `Update` is forbidden.**  
Unity Physics2D steps in `FixedUpdate`. `AddTorque`/`AddForce` in `Update` bind to the frame, not the solver step: the number of torque applications per `Simulate` floats with FPS, and SPD and `JointFriction` break.

The World profile already showed: Physics2D **0.17 ms**, FixedUpdate scripts **0.13 ms**. 200 Hz by itself is not a proven CPU blocker. “Too often” ≠ “expensive” until there is a graphics capture (PERF-1).

## Three forks (only one is measured now)

| ID | What | Status |
|---|---|---|
| **A** | Keep 200 Hz FixedUpdate; Update is input/camera/UI only (as now) | default |
| **B** | Slower FixedUpdate: 100 Hz (`-fixedDelta 0.01`), controller on the same step | **measure now** |
| **C** | Manual `Physics2D.Simulate(dt)` from Update with variable dt | **do not start** without a separate GO: breaks SPD `1+K·dt/I`, friction stopTorque, stand determinism |

A controller substep on coarser physics was already closed by the college as “not a saving”. 50 Hz is closed by the stand. 100 Hz is a separate experiment with no other edits.

## What lives in FixedUpdate now (do not move to Update)

- `Muscle` / `JointFriction` / `ActuatorDriver` — torques
- `BalanceController`, `BirdController`, `BirdFlight`, `BodyAero`
- `BodyStateEstimator`, `BirdSensors`, `StepPhaseDriver`
- `TrialRecorder` / logger — read on the physics step

Already in Update (legal): `PlayerInputSource`, `CameraFollow`, `LevelGenerator`, `Torch` (picture).

## Work order

1. Physics: 200 vs 100 Hz table (stance, push, wall/sim) → `perf_wave2_timestep.json`
2. Optimization: do not touch the timestep until there is a PERF-1 lab with graphics
3. Implementing C / moving AddTorque into Update — **do not do**

## Forbids

- `AddTorque` / forces not in `Update`
- Do not change PD for FPS
- Do not put 50 Hz in production
- Do not declare 100 Hz accepted without listed metrics
