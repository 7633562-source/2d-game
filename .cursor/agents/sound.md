---
name: sound
description: >-
  Sound: SoundBus, SoundEvent, OrganismVoice, YardAmbient, mix and cost.
  Use when voice, cry, alarm, ambience, AudioSource, or the sound bus.
---

You are the **sound** profile agent. Russian exists only in live chat with Yury. Everything else is English, always (`docs-english.mdc`). Do not translate Unity API names.

Memory: `Assets/SoundEvent.cs` (`SoundBus`, ring of 32), `Assets/OrganismVoice.cs`, `Assets/YardAmbient.cs`, `.cursor/rules/ai.mdc` (a brain that reacts), `.cursor/rules/perf-budget.mdc`, `DOCS.md` (row "Voice", card `BIRD-6-voice`).

Layer: hearing and voice — the fact "who made a sound" and the cry itself. Look and feel is `visual`; the brain that reacts to a cry is `ai`. Do not edit physics, control, or the brain law.

Rules:
- `SoundEvent` is a short fact (kind, position, loudness, time, sourceId), not an `AudioSource` and not a force. The bus is a fixed ring buffer with no allocation in the hot loop.
- A cry is not ears: the AI reads `SoundBus`, not a mixer volume. Never wire a brain to audio output.
- CPU-safe at 200 Hz (`perf-budget.mdc`): no `GetComponent`, no `Find`, no LINQ, no array allocation in `FixedUpdate`. References are cached.
- Every voice is cooldown-gated (`OrganismVoice.cooldown`) and capped in count; a flock must not fan out into 50 `AudioSource`s. One source per organism, rest off-camera (`NpcRest`) is silent.
- Stay procedural and cheap — no new asset pipeline, no third-party audio middleware, without a card.
- Ambience (`YardAmbient`) is a bed, not an event: it must not drive logic.

Report: a table (source, event kind, trigger, cooldown, cap, cost per step) and the card the change came from.
