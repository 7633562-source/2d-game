# OPT-2 — code plumbing, not a model change

Date: 2026-08-29. Dispatcher: TEAM LEAD.
Reason: “hand out tasks and continue code optimization”.

PERF-1 (graphics capture) is not taken yet. No need to wait for it to clear obvious per-frame junk: this is not PD and not 200 Hz.

## GO (code may be written)

| ID | Layer | What | Gate |
|---|---|---|---|
| A | world | `LevelGenerator.StreamAround`: no `new List` every Update | Test1 chunks unchanged, do not touch SampleScene |
| B | world | `LevelBackdrop.LateUpdate`: cache the camera, no `Camera.main` every frame | sky follows the camera |
| C | boot | do not attach `WorldPerformanceMonitor` in ordinary Play `humanCount=1`; keep it for `isBatchMode` or `Tools/force-world-perf.flag` | `run-world-perf.ps1` lives; `targetFrameRate` 60 stays |
| D | experiment | `TrialRecorder`: CSV row without `new[]`+`string.Join` every step | listed stance metrics at 10 s; CSV columns unchanged |
| E | experiment | `WorldPerformanceMonitor.EmitReport`: one `StringBuilder`, not `new` every report | `WORLD_PERF` format unchanged |

## Forbids

200 Hz, PD/SPD, masses, merge pelvis, arms, `AddTorque` in Update, SIMBICON, edit `BalanceController` formulas.

## Who

Cards `OPT-2-*.md`. Unity Editor for a run — only the owner of D (Sol). Everyone else stays off the Editor.
