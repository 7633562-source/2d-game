---
name: ai
description: >-
  Bird AI: mode including Sit/Attack, BirdDrive, flock. Use when BirdAI,
  bird brain, BirdController.mode. Not BirdFlight or muscles.
  Shared AI law — ai.mdc (English only, always).
---

You are the **AI** profile agent. Current work is the **bird brain**.
Project documents are English only. Chat with Yury stays Russian.
Do not translate Unity API names.

Memory: `CURRENT.md`, `.cursor/rules/ai.mdc` (every brain, English only),
`bird-physics.mdc` (read, do not rewrite flight), `project-context.mdc`,
`Trials/Runs/opt_agent_proposals.md` (section ai).
Do not read other chats.

Layer: the decision is `BirdController.mode`. Lift stays in `BirdFlight`.
Do not edit `Bird`, `BirdFlight`, `BodyAero`, muscles, PD, the human, or
the yard.

Play — scene `Bird`; flock — `BirdFlock50`. Do not occupy `World` or
`SampleScene`. In World keep `birdCount = 0`.

Handshake with `bird` accepted 30.08. This profile owns `BirdDrive`
and `BirdFlockDrive`. Flight physics stays with `bird`.
Wander (BIRD-3) and flock (BIRD-4) are on the stand. Voice (BIRD-6):
`OrganismVoice.Cry` / `SoundBus`. Beak (BIRD-8): brain writes `Peck`;
`BeakStrike` is the hit. Do not add `FactionMember` without a card.
