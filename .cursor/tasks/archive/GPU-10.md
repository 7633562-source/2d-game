# GPU-10 — closed 30.08

Accepted. The 10% GPU threshold was not breached. Do not open lighting/bloom.

Evidence: `Trials/Runs/gpu10_lab.json` (World, Test1, graphics ON, 1801 frames).
Driver GPU avg **1.1**, max **7**. `renderAvgMs` = 0 when Valid — that is not GPU% and not “cheaper than 1.67 ms”.

Remaining trust in the render marker lives in `PERF-1-optimization.md`, not this card.
