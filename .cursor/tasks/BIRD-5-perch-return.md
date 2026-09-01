# BIRD-5 — return to the perch

Agent: `ai`. Writes `mode` / `SetFacing` only. Play: `BirdFlock50` with a tree.
Stay out of `World` / `SampleScene`. Do not edit `BirdFlight` or wood springs.

## One sentence

After a flight the bird sits on its tree slot again. Dirt is not home:
face the slot and take off.

## Contract

`PlantTree.NearPerch` / `GetPerchSlots` / `BindPerch(es)` only.
No `AddForce`. Glide `SetFacing` toward `homeX` only when
`|x − homeX| > homeFaceDeadzone` (default 0.15 m). Headless:
`-bird 1 -birdRig flock -birdDrive wander -tree 1 -treePerch 1`.

## Accept

30 s solo or 20 s flock of 5: `applied.perchLandCount` ≥ 1,
`takeoffCount` ≥ 1. Pitch fall still counts. Sitting on dirt after a
crown start may still set `bodyDrop`; Fly/Glide on dirt must not.
