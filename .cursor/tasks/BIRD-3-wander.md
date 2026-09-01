# BIRD-3 — random wander

Agent: `ai`. Writes `mode` / `SetFacing` only. Play: `Bird`.
Stay out of `World` / `SampleScene`.

## One sentence

The bird sits, takes off, turns along ±X, glides, lands, repeats.
Seeded random, not a chase.

## Contract

`BirdDrive.kind = Wander` plus `BirdWander`. No `AddForce`. No Vision.
Headless: `-bird 1 -birdRig flock -birdDrive wander -birdDriveSeed 1`.

## Accept

30 s: `fell` false, `sitFraction` and `flyFraction` both > 0,
`takeoffCount` ≥ 1, `landCount` ≥ 1, `facingFlipCount` ≥ 1.
`applied.birdDriveKind` is Wander.
