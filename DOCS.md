# Document map

Document manager: agent `docs` (`.cursor/agents/docs.md`).
Live chat door: `CURRENT.md`. This file is pointers, not the body journal.

**Language:** English only, always. Rule: `.cursor/rules/docs-english.mdc`.

Nightly check: Windows task `Unity-Docs-daily` at **02:00** local time,
script `Tools/run-docs-daily.ps1`, card `.cursor/tasks/DOCS-DAILY.md`.
Do not put this on Cursor cloud Automations: no git, the VM cannot see the
folder (same as GPU-10). Night report: `Trials/Runs/docs_daily/` and
`Trials/Runs/docs_daily_latest.md`.

## Where truth lives

| Question | File | Do not take it from |
|---|---|---|
| What is open today | `CURRENT.md`, `.cursor/tasks/OPEN.md` | old chats |
| Body numbers and gains | `.cursor/rules/current-front.mdc` | `Trials/Baselines/current/` |
| Stance acceptance | `Trials/Baselines/stable-pd/` | the folder named `current/` |
| How to run the stand | `.cursor/rules/trial-protocol.mdc`, `Trials/README.md` | Play Mode |
| World frame cost | `Trials/Runs/perf_wave1_lab.md` (+ `.json`) | `renderAvgMs = 0`, `busyMain` |
| What each agent may optimize | `Trials/Runs/opt_agent_proposals.md` | cutting 200 Hz, retuning PD |
| Latest optimization verification | `Trials/Runs/opt_code_audit_31.md` | invalid Flat cycles 31/32 |
| Bird Flock capacity | `Trials/Runs/perf_bird_flock_01.md` | ragdoll birds, empty callback candidate |
| Torque signs | `.cursor/rules/balance-actuators.mdc` | guessing from the angle sum |
| Agent roster | `.cursor/ROSTER.md`, `AGENTS.md` | college in `~/.cursor/agents/` |
| Brains | `.cursor/rules/ai.mdc` | forces, PD, `BirdFlight` |
| Voice | `OrganismVoice` / `SoundBus` (card `BIRD-6-voice`) | mixer as ears |
| Beak | `Beak` segment + `BeakStrike` (card `BIRD-8-beak`) | chest / wing hit |
| Tree sit | `BirdPerch` pads, wood ghost (`BIRD-9-tree-ghost`) | landing on trunk colliders |
| Dog stance | `.cursor/rules/dog-physics.mdc`, `Trials/Runs/dog_cycles.md` | human `BalanceController` |
| Dog look | `Assets/Resources/Art/Dog/`, card `VIS-DOG-textures` | dog sizes / stance gains |
| Bird look | `Art/Crow/`, `Art/Chicken/`, card `VIS-BIRD-textures` | `BirdFlight` / `BirdKind` sizes |
| Painted parts | `.cursor/rules/painted-parts.mdc` | judging crops instead of Play |

`current-front.mdc` is a journal. **Append** newly accepted numbers. Do not
scrub old paragraphs.

## Do not delete or rename

The folders below are trial memory. "Cleaning Runs" deletes proof.

| What | Why |
|---|---|
| `Trials/Baselines/*` | frozen baselines; name `current/` is **historical** |
| `Trials/Runs/*` | working summaries; the label is provenance |
| `.cursor/tasks/archive/` | closed waves |
| `E:\Games\Unity3D LOGS\trials` | CSV/JSON from before the repo copy |

Do not rename `Baselines/current/` for clarity: rules and old reports point
at that name. Read it as "ostrich knee".

This project is **not a git repo**. There is no version history besides
copies on disk. Deletion cannot be undone.

## Baselines (`Trials/Baselines/`)

| Folder | Status | What is inside |
|---|---|---|
| `stable-pd/` | **acceptance** | SPD, runs `s_on` / `son_*` |
| `pelvis-world/` | history | pelvis to world upright, before SPD |
| `knee-flipped/` | history | knee `0…+120`, pelvis not yet world-controlled |
| `current/` | history | `curr_*`, ostrich knee; do not read the name as "current" |
| `legacy-no-provenance/` | history | `damp_*` with no fingerprint; do not backfill provenance |

A new baseline only if a card said so. Otherwise the anchor is `stable-pd/`,
fresh numbers from `Trials/Runs/<label>.json` → `applied`.

## Rules (`.cursor/rules/`)

Always in chat: `project-context`, `current-front`, `trial-protocol`,
`agent-handoff`, `ai`, `docs-map`, `docs-english`, `perf-budget`.
By layer: `balance-actuators`, `unity-2d-physics`, `level-generation`,
`lighting-2d`, `bird-physics`, `plant-physics`, `dog-physics`, `character-ai`.

## Known contradictions (do not erase; fix the pointer)

1. **Baseline.** Root `README` and `Trials/README` used to name
   `Baselines/current/` as canon. Since 2026-08-30 the pointer is
   `stable-pd/`. The `curr_*` files stayed on disk.
2. **Hand.** In `Human.cs` default `handSize.y = 0.32`. Card `VIS-HAND`
   still asks for `0.19`. `current-front` records that `0.19` folded walk
   at ~25 s and the physical length was restored to 0.32. Stance `vis_hand`
   at `0.19` was accepted on its own. Do not close the card silently.
3. **CSV.** `stable-pd/` and `knee-flipped/` have JSON+CSV side by side.
   `current/`, `legacy-no-provenance/`, `pelvis-world/` are mostly JSON in
   the project. Full CSV lives in the external log folder.

## Tools

Stand and measure scripts: `Tools/README.md`.
