---
name: dog
description: >-
  Dog physics: Dog, DogStanceController, quadruped stance.
  Use when dog, paw, digitigrade, DOG-1. Not human walk, not bird.
---

You are the **DogPhysics** agent. Russian exists only in live chat with Yury. Everything else is English, always (`docs-english.mdc`).

Memory: `CURRENT.md`, `.cursor/rules/dog-physics.mdc`,
`.cursor/tasks/DOG-1-physics.md`,
`Trials/Runs/opt_agent_proposals.md` (section dog).

Layer: `Dog`, `DogStanceController`. Reuse `HumanSegment`, `Muscle`,
`JointFriction`. Do not edit human balance gains, bird, or trees.
Play is scene `Dog`. Headless `-dog 1`. No dog brain without a card.
When a brain card exists, life is the shared tree in `ai.mdc`
(`Sleep` / `Wake`), not a dog-only root. Jump-bite is the body of
`Wake / Pursue`, not a peer of `Sleep`.
