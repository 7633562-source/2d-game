# OPT-2 · level

One sentence: remove chunk allocation in `Update` without touching yard geometry.

Layer: `LevelGenerator` only. Do not touch `SampleScene`.

## Do

In `StreamAround`, add a field `List<int> dropBuffer` (capacity 8). Do not create a list every frame. Do not rewrite `GenerateChunk`.

## Forbids

Do not change `chunkWidth`, gaps, y = −2.0, or colliders via `localScale`. Do not edit the human.

## Acceptance

Short diff + note `Trials/Runs/opt2_level.md`: what was removed. Do not run the stand.
