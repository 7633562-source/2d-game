# PERF-1 · 2D level generation

One sentence: need the cost of the Test1 yard versus flat ground — gaps and extra contacts kill a ragdoll more than FPS.

Layer: world (`LevelGenerator`, `StaticPlatform`, `LevelTest1`). Do not touch `SampleScene`. Do not change the human, muscles, or bird.

## Contract

- Spawn surface y = −2.0, human y = −0.82.
- No gaps between pieces.
- Chunk generation in `Update`, not `FixedUpdate`.
- Platform size is `sprite.size` / `collider.size`, not `localScale`.

## Do

1. Count for Test1 and Flat: static colliders in frame, foot contacts, chunk-generation calls over 30 s.
2. Check whether the yard breeds extra probes/contacts versus `meanProbeHits` after cycles 3–4.
3. If the level is expensive — a list of forks (fewer curbs, merge statics, layer mask). Do not change code in this card except writing the summary.

## Forbids

Do not hang the generator on `SampleScene`. Do not make `course`/`steps` the default. Do not edit balance.

## Acceptance

`Trials/Runs/perf_wave1_level.json`: Test1 vs Flat, colliders, contacts, conclusion. Table of numbers.
