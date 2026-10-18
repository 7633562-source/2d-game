# PERF-1 · optimization

One sentence: the frame lab must become trustworthy, or any
"optimization" can kill the model blind.

Layer: experiment (`WorldPerformanceMonitor`, `Tools/run-world-perf.ps1`,
summaries). Do not edit physics, muscles, PD, levels, or bird.

## Contract

- Do not rename `WorldPerformanceMonitor` public fields.
- Do not break the human headless stand.
- `Physics2D jobs` may stay ON in the game; in `HeadlessTrial` jobs OFF.
- Do not start a new balance baseline.

## Do

1. ~~Capture Play Mode `World` with graphics~~ — `Trials/Runs/gpu10_lab.json`
   exists (Test1, 1801 frames, jobs ON).
2. ~~Into the summary: p50/p95/p99, Physics2D, scripts, render,
   `maxFixedSteps`, GC.Alloc~~ — plus honesty flags:
   `renderMarkerEmpty`, `busyMainTrusted`, `knownWorkAvgMs`.
   **renderAvgMs is still 0** while Valid; that is now labeled empty.
3. ~~Repeat Flat and Test1~~ — `world_perf_capture_flat.json` /
   `world_perf_capture_test1.json`. Yard − Flat ≈ 0.01 ms physics.
   The level does not eat the frame.
4. ~~Fix metric trust~~ — `busyMain` in batch is the 60 FPS cap;
   render=0 is not cost; driver GPU% is not render ms.
   URP camera CPU and FrameTiming stay **−1** in Editor batch (blocker).
5. ~~Concept forks~~ — numbered list in `perf_wave1_lab.json`.
   Lights/bloom closed by the GPU-10 threshold. No fork code.

## Follow-up 31.08

6. ~~Reject stale captures~~ — `run-world-perf.ps1` now requires a
   successful exit, current-log `WORLD_PERF_JSON`, fresh capture
   timestamp, report samples, and capture samples. Failed runs write
   `valid = false`, keep `capture = null`, and do not copy named captures.
7. ~~Terminate only owned timeout~~ — the runner terminates the
   disposable Unity process tree it started after 180 s. A forced
   timeout was not staged.
8. ~~Measure script phases honestly~~ — FixedUpdate, Update, and
   LateUpdate now have separate averages, validity flags, and source
   names. `knownWorkAvgMs` includes all three.

Verification: `Trials/Runs/world_perf_summary_cycle33.json`.
`valid = true`; Fixed / Update / Late scripts =
0.121 / 0.016 / 0.037 ms; known work = 0.485 ms.
The first Flat attempt was refused because the Library was in use. The
second (`world_perf_summary_cycle34.json`) exited -1 before Play and
correctly recorded zero samples, `valid = false`, and `capture = null`;
it left no fixed-name capture or Unity process and did not overwrite the
preserved named Flat capture.

## Follow-up 01.09

9. ~~Safe P2/P3 cleanup~~ — runtime caches, shared flock hearing,
   direct bird/dog CSV appends, allocation-free Dog activation/Falling,
   center-chunk streaming, and a reused trial fixed-step yield are
   implemented. Card:
   `.cursor/tasks/archive/PERF-4-runtime-cache-cleanup.md`.
10. ~~Re-shoot current World+grove~~ — cycle 39 is valid: p95 16.75 ms,
    Physics2D 0.270 ms, Fixed / Update / LateUpdate
    0.097 / 0.019 / 0.029 ms, known work 0.414 ms,
    `maxFixedSteps = 4`.
11. ~~Remove Static-body warning flood~~ — `LevelGrove.PinToYard`
    no longer writes velocities after `TreeRig.Static` has already made
    a body Static. Cycles 38/39 contain none of the former 180 warnings
    and exit cleanly.

Cycle 35 crashed before Play on a transient 1.4 MB texture allocation;
the immediate cycle-36 retry succeeded. Flat cycle 37 timed out before
Play. Both invalid runs contain zero samples and no capture and left no
Unity process. Cycle 38's GC marker outlier did not repeat: unchanged
cycle 39 returned to 28.1 bytes/frame. Do not compare invalid runs.

## Forbids

200 Hz, PD, masses, merging segments, amputating arms, a crowd in World,
100 Hz as the next step.

## Acceptance

File `Trials/Runs/perf_wave1_lab.json` plus a short table in the reply.
Colleague note (English): `Trials/Runs/perf_wave1_lab.md`.
No CSV in chat. If graphics cannot be captured — write the blocker
honestly and what was not measured.
