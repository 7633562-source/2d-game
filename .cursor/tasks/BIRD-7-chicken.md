# BIRD-7 — chicken on the same Bird body

Agents: `bird` (recipe, mass, stance height) + `ai` (`ChickenWander`).
Play: `Chicken`. Stay out of `World` / `SampleScene`.

## One sentence

`Bird` is the family. Crow stays the flying canon. Chicken is a ground
recipe: Sit / Walk, a hop is not a cruise.

## Layer

Same `Bird` + `BirdKind`. Not a child of `Human`. No `BirdIntent`.
Brain writes `mode` / `SetFacing` only. Ground peck is `BirdMode.Peck`
(card `BIRD-8-beak`).

## Contract

- `BirdKind.Crow` (0) — current flight numbers.
- `BirdKind.Chicken` (1) — 1.8 kg, short wings, `hoverMean` 0.55.
- Play scene `Assets/Scenes/Chicken.unity` (`birdKind` chicken, 5 birds).
- Headless: `-birdKind chicken`.
- Spawn Y after `ApplyKind` via `StandingRootOffset`, not crow `StandingRootY`.

## Accept

20 s flock wander: `fell` false, `sitFraction` and `walkFraction` > 0,
`flyFraction` ≈ 0, `maxBodyY` not a cruise (stay near stance),
`applied.birdKind` Chicken, `hoverMean` 0.55.
Crow without `-birdKind` must not change.

Stand `Trials/Runs/bird_chicken.json` (20 s, flock wander, seed 1):
`fell` false, sit 0.413 / walk 0.587 / fly 0, `bodyDrop` 0,
`maxBodyY` −1.801 (stance), `walkCount` 4, `hoverMean` 0.55,
`applied.birdKind` Chicken. Crow re-run after this build hit STALE
from a colleague edit — do not treat as a crow change.

## Forbid

Do not retune crow defaults. Do not cut 200 Hz. Do not touch human PD.
Do not put chickens in `World`.
