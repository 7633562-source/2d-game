# BIRD-9 — fly through wood, sit on pads

Agents: `bird` (`GhostTreeWood`) + `plant` (`BirdPerch` pads).
Play: `BirdFlock50` with a tree. Stay out of `World` / `SampleScene`.

## One sentence

A bird does not collide with tree wood. It flies through the crown and
sits only on the tree's perch points.

## Contract

- `Bird.GhostTreeWood` ignores bird colliders vs wood, not vs `BirdPerch`.
- `PlantTree.EnsureBirdPerches` builds up to 20 Ground pads at
  `GetPerchSlots` (twigs first). No extra `Rigidbody2D` on a pad.
- Brain still writes `mode` / `SetFacing` only. No `AddForce` into wood.

## Accept

`-bird 1 -birdRig flock -birdDrive wander -tree 1 -treePerch 1`:
`perchLandCount` ≥ 1 or Sit on a slot, `fell` false. Flat wander
without `-tree` must not change.

Accepted 01.09 on build fingerprint `4bb32153dd835e3f…`:
`perf_flock_metric_tree1` ran 20 s with `perchLandCount = 5`,
`dirtRejectCount = 7`, and `fell = false`. Flat 50 behavior remained
bitwise equal in CSV after the recorder-only `enteredFlight` gate.
Full evidence: `Trials/Runs/perf_bird_flock_01.md`.
