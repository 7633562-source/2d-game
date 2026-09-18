# OPT-2 · optimization

One sentence: the profiler must not sit in ordinary play; the monitor report must not allocate a new StringBuilder.

Layer: `GameProcess`, `WorldPerformanceMonitor`. Do not touch muscles or PD.

## Do

1. `GameProcess.Start`: `AddComponent<WorldPerformanceMonitor>` only if `Application.isBatchMode` **or** the file `Tools/force-world-perf.flag` exists. Keep `targetFrameRate = 60` and `vSyncCount = 0` for World `humanCount=1` as they are.
2. `WorldPerformanceMonitor.EmitReport`: field-`StringBuilder`, `Clear()`, same text `WORLD_PERF ...`.
3. `run-world-perf.ps1` / `WorldPerfBatch.Run` must still receive the monitor (batchmode).

## Forbids

Do not drop 200 Hz. Do not tune PD. Do not delete the monitor class.

## Acceptance

`Trials/Runs/opt2_optimization.md`. Do not run the human headless stand (`GameProcess` early-returns there).
