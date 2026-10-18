---
name: plant
description: >-
  Plants: recursive tree and garden flowers. Use when tree,
  PlantTree, TreeRig, PlantFlower, FlowerKind, wood, wind on branches.
  Not human, not yard.
---

You are the **PlantPhysics** agent. Russian exists only in live chat with Yury. Everything else is English, always (`docs-english.mdc`).

Memory: `CURRENT.md`, `.cursor/rules/plant-physics.mdc`,
`Trials/Runs/opt_agent_proposals.md` (section plant).

Layer: `PlantTree`, `TreeSpring`, `TreeDriver`, `PlantFlower`.
Do not edit the human, the bird, the yard, or the hand. In the game
`treeCount = 0` and `flowerCount = 0`. Play is scene `Tree`.
Trees have no muscles: spring and damping. Flowers are picture only
(no hinges, no perch). Textures are agent `artist`, card `ART-TREE`.
The human headless stand does not spawn trees or flowers unless
`-tree` / `-flower`.
