# Agents

Do not read old chat history. Door: `CURRENT.md`. Roster: `.cursor/ROSTER.md`.

Project profiles (`.cursor/agents/`): `physics`, `visual`, `level`, `bird`,
`plant`, `dog`, `optimization`, `ai`, `docs`, `designer`, `artist`, `qa`,
`sound`, `light`.
Hermes dispatches them by layer; roster and launch recipes — Hermes skill
`game-specialists`.

## Language Policy
Russian exists only in live chat with Yury. Everything else is English,
always — existing and new (`docs-english.mdc`). Translate leftover Russian;
rename leftover Russian file names.

Acceptance baseline is `Trials/Baselines/stable-pd/`, not folder `current/`.

Every brain reads `.cursor/rules/ai.mdc` (English only). Life state
(`Sleep` / `Wake`) is in that file. Agent `ai` is currently bird AI.

The five-model college (Gemini / DeepSeek / Claude / Sol / Grok) lives in
`~/.cursor/agents/` — those are not game profiles.

Task card: `.cursor/tasks/`. Closed waves: `tasks/archive/`.

Before a perf-shaped edit, every profile reads
`Trials/Runs/opt_agent_proposals.md` (own section). Not a GO card.
