# PERF-1 · 2D visual

One sentence: need to measure picture cost (URP 2D, lights, bloom, sprites), because batch World did not catch render.

Layer: visual (`HumanSegment`, `SceneLighting`, `Torch`, URP 2D). Do not touch physics, muscles, level generation, or the bird.

## Contract

- Do not replace colliders and `sprite.size` with `localScale`.
- A purely visual edit must give the same CSV as before. This card has no picture edits — measurement and an options list only.
- Do not touch `SampleScene`.

## Do

1. Play Mode `World` with graphics: 1 human, Test1, lighting as now.
2. Break down the frame: Camera/URP, Light2D, bloom, sprite overdraw, number of active lights.
3. Compare night+torch vs no lights if a switch already exists; otherwise estimate the cost of one `Torch` and the global 2D light.
4. Forks as a list only: fewer lights, weaker bloom, cheaper sprite, deferred light. Do not change code.

## Forbids

Do not tune PD. Do not turn off `Interpolate`. Do not “optimize” segments with scale. Do not edit `BalanceController`.

## Acceptance

`Trials/Runs/perf_wave1_render.json`: ms render / lights / sprites, Light2D count, verdict “picture eats the frame / does not”. Table, no CSV.
