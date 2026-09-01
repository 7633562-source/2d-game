# Open cards

| File | Agent |
|---|---|
| `WALK-1-physics.md` | physics |
| `VIS-HAND.md` | visual (stance accepted; Play after build) |
| `PERF-1-optimization.md` | optimization — lab + `opt_agent_proposals.md` (pre-grove) |
| `PLANT-1-tree.md` | plant (sway tree in scene `Tree`) |
| `VIS-TREE.md` | visual (bark/leaf on; Play scene `Tree`) |
| `VIS-DOG-textures.md` | visual — painted dog parts, sizes unchanged |
| `VIS-BIRD-textures.md` | visual — crow and chicken parts, flight unchanged |
| `DOCS-DAILY.md` | docs (nightly 02:00) |
| `DOG-1-physics.md` | dog — quadruped stance, 10 stand/opt cycles |
| `BIRD-2-sit-attack.md` | bird + ai — Sit / Attack; prey `BirdDrive` |
| `BIRD-3-wander.md` | ai — random Sit / Fly / turn / land |
| `BIRD-4-flock.md` | ai — `BirdFlockDrive` |
| `BIRD-5-perch-return.md` | ai — sit on the tree slot again |
| `BIRD-6-voice.md` | ai — vocalize; threat → Alarm then Fly |
| `BIRD-7-chicken.md` | bird + ai — `BirdKind.Chicken`; Play `Chicken` |
| `BIRD-8-beak.md` | bird + ai — beak segment; hen Peck; BeakStrike |
| `BIRD-9-tree-ghost.md` | bird + plant — fly through wood; sit on `BirdPerch` |

`ai` — bird brain; wander, flock, voice, hen, beak, perch (`BIRD-3`…`BIRD-9`).

`docs` — standing role: `DOCS.md`, pointers to `stable-pd/`, do not delete
summaries. Night 02:00: `Unity-Docs-daily` → `Tools/run-docs-daily.ps1`.

Every profile also reads `Trials/Runs/opt_agent_proposals.md` (own
section). Not a GO card.

Everything else is `archive/`.
Documents are English only, always.
