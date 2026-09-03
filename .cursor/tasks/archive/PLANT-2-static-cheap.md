# PLANT-2 · Static grove without wood physics

One sentence: a Static tree is a picture plus sit pads. Hundreds of
segments must not enter Box2D. Birds still sit on `BirdPerch`.

Layer: plants (`PlantTree`, `LevelGrove`). Do not retune Sway springs,
human PD, or 200 Hz.

## Contract

- `TreeRig.Sway` (scene `Tree`, headless `-tree 1`) stays hinges +
  `TreeSpring` + wind. Hold numbers must not move.
- `TreeRig.Static` (yard grove, Tree-scene extras) has **no**
  `Rigidbody2D` and **no** wood collider. Size is still `segment.size`,
  not `localScale`.
- Sit contact is only `BirdPerch` pads (Ground, no extra RB, cap 20,
  twigs first). `GetPerchSlots` / `SeatBirds` / `NearPerch` stay.
- Wood vs world / human / bird stays off. Do not restore
  `IgnoreCollision` pairing as the isolation method.
- Default oak recipe stays 15 segments. Static may grow past the old
  31-body clamp; Sway stays capped.

## Acceptance

- Sway empty hold: `maxAbsJointAngle` ≈ 0.002°, `crownDrop` 0.
- Static oak: `treeRigidbodyCount` 0, `treeWoodColliderCount` 0.
- Static + bird + `-treePerch 1`: pads exist, `perchLandCount` ≥ 1 or
  Sit on a slot, `fell` false on the one-bird tree run.
- World grove plants Static. Do not plant Sway in World.

Accepted 02.09 on fingerprint
`d201393a639e6b72c121fd4911bd03cfa5c80171ddd60d64ed0ecef13cea4699`.
`plant_sway_hold` 0.00187° / drop 0, 16 wood RB. `plant_static_oak`
0 / 0 / 0. `plant_static_perch` pads 20, `perchLandCount` 7,
`fell` false. Report: `Trials/Runs/plant_static_cheap_02.md`.
