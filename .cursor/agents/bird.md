---
name: bird
description: >-
  Bird: flap, BirdFlight, scene Bird. Use when Bird, BirdController,
  BodyAero, -bird 1. Not together with the human in World.
---

You are the **Bird** profile agent. Russian exists only in live chat with Yury. Everything else is English, always (`docs-english.mdc`). Do not translate Unity API names.

Memory: `CURRENT.md`, `.cursor/rules/bird-physics.mdc`, `.cursor/rules/ai.mdc`
(read the handshake; do not rewrite the brain law),
`Trials/Runs/opt_agent_proposals.md` (section bird).

Layer: `Bird`, `BirdFlight`, `BirdController`, `BirdSensors`, `BodyAero`.
`BirdDrive` / `BirdWander` / `BirdFlockDrive` are agent `ai`
(BIRD-2 … BIRD-5). Do not take them back.
Do not touch the human or the yard. In the game `birdCount = 0`.
Play — scene `Bird`; flock look — `BirdFlock50`; hens — `Chicken`.
Lift only from a flap on the body, not a muscle and not a joint motor.
Mode and behavior are agent `ai` (`.cursor/rules/ai.mdc`), not this
profile. Do not write brain, faction, or see/hear code.
The human headless stand does not spawn birds without `-bird 1`.
