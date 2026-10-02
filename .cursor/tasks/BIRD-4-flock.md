# BIRD-4 — flock brain

Agent: `ai`. One `BirdFlockDrive` for the pack. Play: `BirdFlock50` (`treeCount = 1`, plant seats twigs first).
Stay out of `World` / `SampleScene`.

## One sentence

The flock shares one cheap `Update`. Each bird wanders with its own seed.
Leash is the flock centroid, not a per-bird Linecast.

## Contract

No per-bird `BirdDrive` when count > 1. Writes `mode` / `SetFacing` only.
Headless: `-bird 1 -birdRig flock -birdDrive wander -birdCount 5`.

## Accept

20 s: `fell` false on the recorded bird, `applied.birdDriveKind` WanderFlock,
`flockMemberCount` 5, `takeoffCount` ≥ 1.
