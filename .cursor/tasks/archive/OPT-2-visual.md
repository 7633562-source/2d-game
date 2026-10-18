# OPT-2 · visual

One sentence: the sky must not look up `Camera.main` 60 times per second.

Layer: `LevelBackdrop`. Do not touch physics.

## Do

Cache `Camera` in `OnEnable`/`Install` (or the first LateUpdate). Do not call `Camera.main` in `LateUpdate`. If the camera is gone — one retry lookup, not every frame.

Do not touch `Torch` and `HumanSegment` (flicker and sprites already live in Update/spawn).

## Forbids

No `localScale` on physical segments. Do not turn off Interpolate. Do not edit URP/bloom “by eye”.

## Acceptance

`Trials/Runs/opt2_visual.md`. Do not run the stand.
