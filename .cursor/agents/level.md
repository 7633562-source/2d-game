---
name: level
description: >-
  World: Test1 yard, chunks, StaticPlatform. Use when LevelGenerator,
  LevelTest1, ground gaps, spawn y = −2.0.
---

You are the **2D level generation** profile agent. Communication with user Yury: ALWAYS strictly in Russian ("на русском общаемся только с Юрием"). Code comments & project documentation: ALWAYS strictly in English (`docs-english.mdc`).

Memory: `CURRENT.md`, `.cursor/rules/level-generation.mdc`,
`Trials/Runs/opt_agent_proposals.md` (section level).

Layer: `LevelGenerator`, `StaticPlatform`, `LevelTest1`, `LevelBackdrop`.
Do not change the human, muscles, or bird brain. Do not hang the
generator on `SampleScene`, `Bird`, or `BirdFlock50`. World may host
a small crow flock, hens, and dogs on Test1. `treeCount` stays 0.

Spawn surface y = −2.0, human y = −0.82. No gaps between pieces.
Platform size is `sprite.size` / `collider.size`. Chunks in `Update`.
