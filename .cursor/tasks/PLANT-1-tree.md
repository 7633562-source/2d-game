# PLANT-1 — recursive tree, physics

One sentence: scene `Tree` has a segmental tree on hinges; wind bends it
and the spring brings it back.

Layer: plants (`PlantTree`, `TreeSpring`, `TreeDriver`).

Contract:
- Do not touch `SampleScene`; `treeCount` stays 0 there.
- Do not plant `Sway` in `World` (cost ≈ one Human).
- No muscles. Do not change human moment signs.
- A segment is a box + `HingeJoint2D`; size is not `localScale`.

Hold: wood gravity is `holdTorque` at the grow pose. Branches must not
sag to the hinge limit. A bird on a branch is extra load — the spring
bends, then returns.

Acceptance (30.08 hold + perch):
- Empty `-tree 1 -treeWind 0` 8 s: `maxAbsJointAngle` **0.002°**,
  `crownDrop` **0** (`tree_hold.json`).
- 8 Sit `-birdRig flock -treePerch 1`: angle **23.6°**, drop **11 cm**
  (`tree_perch8.json`).
- 50 Sit (twigs first): angle **24.6°**, drop **7 cm** (`tree_perch50.json`).
- Play `BirdFlock50` (`treeCount = 1`) seats on `GetPerchSlots` (twigs first).

Kinds (31.08): `TreeKind` Oak / Pine / Willow / Bush / Poplar.
Play `Tree` is a row of five (first Sway, rest Static). Yard grove
cycles the same kinds on `Static`. CLI `-treeKind`.

| Kind | Empty hold | Segments | Note |
|---|---|---|---|
| Oak | 0.002° / drop 0 | 15 | canon, same as 30.08 |
| Pine | 0.0004° / drop 0 | 6 | tall, sparse sides |
| Willow | ~11° mean / drop 8 cm | 15 | weeps; not a slam |
| Bush | 12.6° / drop 0 | 10 | short, three-way root |
| Poplar | 0.003° / drop 0 | 15 | tall thin |

Textures are not this card (`VIS-TREE`).
