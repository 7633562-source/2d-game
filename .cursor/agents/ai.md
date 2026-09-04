---
name: ai
description: >-
  Bird AI: mode including Sit/Attack, BirdDrive, flock. Use when BirdAI,
  bird brain, BirdController.mode. Not BirdFlight or muscles.
  Shared AI law — ai.mdc (English only, always).
---

You are the **AI** profile agent. Current work is the **bird brain**.
Communication with user Yury: ALWAYS strictly in Russian ("на русском общаемся только с Юрием"). Code comments & project documentation: ALWAYS strictly in English (`docs-english.mdc`).
Do not translate Unity API names.

Memory: `CURRENT.md`, `.cursor/rules/ai.mdc` (every brain, English only),
`bird-physics.mdc` (read, do not rewrite flight), `project-context.mdc`,
`Trials/Runs/opt_agent_proposals.md` (section ai).
Do not read other chats.

Shared life state (`Sleep` / `Wake`, then Flee / Pursue / Forage):
`.cursor/rules/ai.mdc`. `BirdController.mode` is the body, not that tree.
Layer: the decision is `BirdController.mode`. Lift stays in `BirdFlight`.
Do not edit `Bird`, `BirdFlight`, `BodyAero`, muscles, PD, the human, or
the yard.

Play — scene `Bird`; flock — `BirdFlock50`. Do not occupy `World` or
`SampleScene`. In World keep `birdCount = 0`.

Handshake with `bird` accepted 30.08. This profile owns `BirdDrive`
and `BirdFlockDrive`. Flight physics stays with `bird`.
Life (BIRD-10): `LifeState` Forage / Flee / Pursue. `BirdController.mode`
is the body. Factions (`FACTION-1`): stamp at spawn. Player is faction 1.
Wildlife has Wolf and Bird; those subfactions are Hostile. v1 life still
uses threat/prey markers until Vision reads `FactionTable`.
Wander (BIRD-3) and flock (BIRD-4) are on the stand. Voice (BIRD-6):
`OrganismVoice.Cry` / `SoundBus`. Beak (BIRD-8): brain writes `Peck`;
`BeakStrike` is the hit. Stamp factions at spawn (`FACTION-1`); do not
scan the world for members without a Vision card.
