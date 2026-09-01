# Cursor roster (recreated 2026-08-30)

Do not read old long chats. A new chat = `CURRENT.md` + your rule + a card
+ `Trials/Runs/opt_agent_proposals.md` (your section). Not a GO card.

Documents are English only, always.

| Agent | Layer | Rule | Play scene |
|---|---|---|---|
| `physics` | body / actuators / human control | `balance-actuators.mdc` | `SampleScene` |
| `visual` | sprite, camera, light | `lighting-2d.mdc` | `World` / `SampleScene` / `Dog` (look) |
| `level` | yard, chunks | `level-generation.mdc` | `World` |
| `bird` | flight (not the brain) | `bird-physics.mdc` | `Bird` / `BirdFlock50` / `Chicken` or `-bird 1` |
| `plant` | recursive tree | `plant-physics.mdc` | `Tree` |
| `optimization` | frame measure, monitor | `perf-budget.mdc` | `World` |
| `ai` | bird brain (now); law for every AI | `ai.mdc` | `Bird` / `BirdFlock50` / `Chicken` |
| `docs` | document map, baselines, pointers | `docs-map.mdc` | no Play; do not write game code |
| `dog` | quadruped stance | `dog-physics.mdc` | `Dog` or `-dog 1` |

Dispatcher is the chat with the human. The executor reads the card, not the
thread.

One Unity Editor per project. Human stand: `Tools/run-trial.ps1`. Do not
enable birds or `Sway` trees in World.

Closed cards: `.cursor/tasks/archive/`.
