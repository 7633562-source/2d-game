# PERF-1 — performance wave

Date: 2026-08-29. Dispatcher: TEAM LEAD.
Mode: do not rush. A mistake at this stage can make the game unplayable on CPU/frame.
The concept may change, but only after numbers and dispatcher acceptance. Do not start implementing a fork without a “GO” card.

## Why this wave

The World profile 28.08 gave Physics2D 0.17 ms and scripts 0.13 ms, but render was not caught in batch, `busyMain` cannot be computed, `greenMainThreadP95` is false. Five margin cycles are closed. From here, do not tune PD, cut 200 Hz, or merge segments “by eye”.

Need a model-cost lab: what the frame costs now, where the margin breaks, which concept forks are legal.

## Order

1. First, measurement only in your layer.
2. Then a fork table with cost and risk.
3. Implementing a fork is approved by TEAM LEAD on a separate card.

## Wave-wide forbids

- Do not drop 200 Hz and do not put 50/100 Hz as production.
- Do not tune `lumbarP`, muscles, viscosity, or other gains for FPS.
- Do not merge pelvis with chest. Do not amputate arms.
- Do not put a ragdoll crowd in `World`.
- Do not leave `birdCount > 0` as the game default.
- Do not declare a stage accepted without JSON on disk.

## Cards

| Chat | File | Layer | Artifact |
|---|---|---|---|
| optimization | `PERF-1-optimization.md` | frame capture / lab | `Trials/Runs/perf_wave1_lab.json` |
| 2D physics | `PERF-1-physics.md` | body / actuators / model memory | `Trials/Runs/perf_wave1_bodycost.json` |
| 2D visual | `PERF-1-visual.md` | URP / lights / sprites | `Trials/Runs/perf_wave1_render.json` |
| 2D level generation | `PERF-1-level.md` | world / chunk contacts | `Trials/Runs/perf_wave1_level.json` |
| Bird | `PERF-1-bird.md` | bird separately | `Trials/Runs/perf_wave1_bird.json` |

## Delivery

Cards were written to disk 2026-08-29. This is the wave contract. Composer tabs were not injected this turn: sidecar-send was rejected by the guard, the `My project` folder in Agents is collapsed. The executor reads the card file, not chat history. Do not occupy Unity in parallel: first optimization and physics (physics without rebuild), then visual/level/bird one at a time when the frame lab is free.

**30.08:** optimization delivered `Trials/Runs/perf_wave1_lab.json` and
the colleague note `Trials/Runs/perf_wave1_lab.md`. Physics / visual /
level / bird PERF-1 cards in this archive stay unfilled unless their own
chat writes those JSON files.
