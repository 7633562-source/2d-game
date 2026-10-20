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

## Code rules (owner's order 03.10; Claude cycles)
- R1 Size: a new file <= 1500 lines; a file already above 1500 lines may not grow (net delta <= 0
  per change). Split by responsibility into partial/class files.
- R2 Comments say what and why in <= 3 lines. No history in code: no cycle/round numbers, run
  names, md5s, dates or past measurements - those live in git log, reports and briefs.
- R3 Flags: a refuted probe leaves with its revert; a kept change becomes the default and the old
  branch is deleted in the same commit. No "default off, kept for the record" branches.
- R4 A behaviour-neutral refactor is accepted only on byte-identical fences (full-CSV md5).
- R5 One responsibility per file; a new feature gets its own file, not new lines in TickStand/TickWalk.
- Game code is written by Claude (claude-lab requests) while the Claude cycles are on; other
  profiles apply, build, run and report only (F:/hermes-data/claude-lab/PROTOCOL.md).
